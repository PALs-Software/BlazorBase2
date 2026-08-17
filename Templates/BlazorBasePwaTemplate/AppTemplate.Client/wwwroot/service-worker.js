// Development service worker. Deliberately inert so the browser always fetches
// fresh assets while developing — service-worker.published.js does the real caching.
self.addEventListener('fetch', () => { });
