self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.svg$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];
const networkOnlyPathPrefixes = ['/api/'];

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));

    /*
      Take over instead of waiting for every tab to close. The default lifecycle keeps a freshly
      installed worker parked while any client still runs the old one, and a plain reload does not
      release it — so a deployed update could sit unused for days while the cached client talked to
      an API that had already moved on.
    */
    await self.skipWaiting();
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));

    // Control the pages that are already open, so the reload below lands on the new assets.
    await self.clients.claim();
}

async function onFetch(event) {
    const requestUrl = new URL(event.request.url);

    if (event.request.method !== 'GET')
        return fetch(event.request);

    if (requestUrl.origin === self.location.origin
        && networkOnlyPathPrefixes.some(prefix => requestUrl.pathname.startsWith(prefix)))
        return fetch(event.request);

    const shouldServeIndexHtml = event.request.mode === 'navigate';
    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    return cachedResponse || fetch(event.request);
}
