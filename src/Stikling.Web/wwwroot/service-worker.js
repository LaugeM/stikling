// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

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
