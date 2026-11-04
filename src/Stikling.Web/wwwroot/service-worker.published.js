// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
// Only the app's own files go through the worker. Calls to the API, Clerk and Sentry go straight to the
// network, so they never keep an old worker busy and hold up a new version taking over.
self.addEventListener('fetch', event => {
    if (isShareTarget(event.request)) event.respondWith(onShareTarget(event.request));
    else if (event.request.url.startsWith(baseUrl.href)) event.respondWith(onFetch(event));
});

// The app shows a "new version ready" bar; this lets its Reload button take over straight away
self.addEventListener('message', event => {
    if (event.data === 'skipWaiting') self.skipWaiting();
});

// The daily notification. The server sends { title, body, tag, url }; the tag makes today's
// replace yesterday's if that is still showing.
self.addEventListener('push', event => {
    let message = {};
    try {
        message = event.data ? event.data.json() : {};
    } catch {
        message = { body: event.data ? event.data.text() : '' };
    }

    event.waitUntil(self.registration.showNotification(message.title || 'Stikling', {
        body: message.body || '',
        tag: message.tag || 'today',
        icon: 'icon-192.png',
        data: { url: message.url || '/' }
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL((event.notification.data && event.notification.data.url) || '/', self.registration.scope).href;

    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of windows) {
            if (client.url.startsWith(self.registration.scope)) {
                await client.focus();
                if ('navigate' in client) await client.navigate(target);
                return;
            }
        }
        await self.clients.openWindow(target);
    })());
});

// Photos shared to the app from Android's share menu arrive as a POST to share-target. They wait in
// the stikling-share-inbox cache (named without the offline-cache- prefix, so an update leaves it alone)
// until the person sends them to a plant on the Add photos page. The bytes are kept as they came, so
// the camera date survives.
async function onShareTarget(request) {
    const scope = self.registration.scope;
    try {
        const form = await request.formData();
        const cache = await caches.open('stikling-share-inbox');
        for (const file of form.getAll('photos')) {
            if (!(file instanceof File) || !file.type.startsWith('image/')) continue;
            await cache.put(`${scope}share-inbox/${crypto.randomUUID()}`, new Response(file, {
                headers: {
                    'Content-Type': file.type,
                    'X-File-Name': encodeURIComponent(file.name),
                    'X-Last-Modified': String(file.lastModified)
                }
            }));
        }
    } catch (error) {
        console.warn('Service worker: could not store shared photos', error);
    }
    return Response.redirect(scope + 'add-photos', 303);
}

const isShareTarget = request =>
    request.method === 'POST' && request.url.split('?')[0] === `${self.registration.scope}share-target`;

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.mjs$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
// The link preview picture is only for other sites, so the app has no use for it offline
const offlineAssetsExclude = [ /^service-worker\.js$/, /^img\/preview\.png$/, /^example\// ];
// Files only some devices use, like the Danish plant names and the libraries that make a time-lapse
// video. Each is cached the first time the device fetches it, and a new version of the app caches it
// straight away if the old one had it.
const onDemandAssets = [ /^data\/everyday-names\.(?!en\.)[a-z]+\.json$/, /^lib\/mediabunny\//, /^lib\/h264-mp4-encoder\// ];
const isOnDemand = url => onDemandAssets.some(pattern => pattern.test(url));

// The registration scope is the folder the app is served from (e.g. /stikling/ on GitHub Pages),
// so this works both at the domain root and in a subfolder.
const baseUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const used = await onDemandInUse();
    const assets = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !isOnDemand(asset.url) || used.has(asset.url));
    const index = assets.find(asset => asset.url === 'index.html');
    const cache = await caches.open(cacheName);
    // index.html first, so a copy from another version stops the install before anything else is stored
    if (index) await cacheIndexHtml(cache, index);
    await cache.addAll(assets
        .filter(asset => asset !== index)
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' })));
}

// Brave on Android has handed over an index.html that didn't match its hash, and since one failed file
// fails the whole install, that phone stayed on its old version for weeks. So index.html gets a second
// try past the browser's own cache. If that doesn't match either, the copy is kept as long as every
// fingerprinted _framework file it loads belongs to this version, so it can't start an older app:
// dotnet.js lists every assembly by its fingerprint, so any change to the app's code renames it.
// What this lets through is a copy whose markup or inline scripts differ while the app is the same,
// such as the browser's own change or index.html from a deploy that only touched index.html. That
// copy stays until the next update, which is better than never updating at all.
async function cacheIndexHtml(cache, asset) {
    for (const mode of ['no-cache', 'reload']) {
        try {
            await cache.add(new Request(asset.url, { integrity: asset.hash, cache: mode }));
            return;
        } catch (error) {
            console.warn(`Service worker: index.html failed its hash check (${mode})`, error);
        }
    }

    const response = await fetch(new Request(asset.url, { cache: 'reload' }));
    const html = await response.clone().text();
    const known = new Set(self.assetsManifest.assets.map(a => a.url));
    const fingerprinted = (html.match(/_framework\/[^"'\s?#)]+/g) || []).filter(url => /\.[a-z0-9]{10}\.[a-z]+$/.test(url));
    // A redirected response can't be served for a navigation, so it would break opening the app
    if (!response.ok || response.redirected || !fingerprinted.length || !fingerprinted.every(url => known.has(url)))
        throw new Error('Service worker: index.html is not from this version');

    console.warn('Service worker: keeping an index.html that differs from its hash but loads this version');
    await cache.put(asset.url, response);
}

// The on-demand files an earlier version of the app has cached, as paths like "data/everyday-names.da.json"
async function onDemandInUse() {
    const used = new Set();
    for (const key of (await caches.keys()).filter(key => key.startsWith(cacheNamePrefix))) {
        for (const request of await (await caches.open(key)).keys()) {
            const path = request.url.startsWith(baseUrl.href) ? request.url.slice(baseUrl.href.length) : null;
            if (path && isOnDemand(path))
                used.add(path);
        }
    }
    return used;
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For all navigation requests, try to serve index.html from cache,
        // unless that request is for an offline resource.
        // If you need some URLs to be server-rendered, edit the following check to exclude those URLs
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    if (cachedResponse)
        return cachedResponse;

    const path = event.request.url.startsWith(baseUrl.href) ? event.request.url.slice(baseUrl.href.length) : null;
    if (event.request.method !== 'GET' || !path || !isOnDemand(path))
        return fetch(event.request);

    const response = await fetch(event.request);
    if (response.ok)
        await (await caches.open(cacheName)).put(event.request, response.clone());
    return response;
}
