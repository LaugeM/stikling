// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// The app shows a "new version ready" bar; this lets its Reload button take over straight away
self.addEventListener('message', event => {
    if (event.data === 'skipWaiting') self.skipWaiting();
});
