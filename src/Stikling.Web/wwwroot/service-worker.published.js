// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

// The app shows a "new version ready" bar; this lets its Reload button take over straight away
self.addEventListener('message', event => {
    if (event.data === 'skipWaiting') self.skipWaiting();
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
// The link preview picture is only for other sites, so the app has no use for it offline
const offlineAssetsExclude = [ /^service-worker\.js$/, /^img\/preview\.png$/ ];
// Files only some devices use, like the Danish plant names. Each is cached the first time the device
// fetches it, and a new version of the app caches it straight away if the old one had it.
const onDemandAssets = [ /^data\/everyday-names\.(?!en\.)[a-z]+\.json$/ ];
const isOnDemand = url => onDemandAssets.some(pattern => pattern.test(url));

// The registration scope is the folder the app is served from (e.g. /stikling/ on GitHub Pages),
// so this works both at the domain root and in a subfolder.
const baseUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const used = await onDemandInUse();
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !isOnDemand(asset.url) || used.has(asset.url))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
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
