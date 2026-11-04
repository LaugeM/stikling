// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Sharing photos to the app works in development too
self.addEventListener('fetch', event => {
    if (isShareTarget(event.request)) event.respondWith(onShareTarget(event.request));
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
