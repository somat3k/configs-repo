/**
 * MLS Trading Platform — Service Worker
 * Strategy map:
 *   Blazor framework files  → CacheFirst  (immutable, versioned hashes)
 *   API calls (/api/*)      → NetworkFirst (trading data must be fresh)
 *   Shell HTML (/)          → StaleWhileRevalidate
 *   Icons + fonts           → CacheFirst with 30-day expiry
 *   Background sync         → queue orders offline, replay on reconnect
 */

const CACHE_VERSION = 'mls-v1';
const SHELL_CACHE   = `${CACHE_VERSION}-shell`;
const FRAME_CACHE   = `${CACHE_VERSION}-framework`;
const ASSET_CACHE   = `${CACHE_VERSION}-assets`;
const FONT_CACHE    = `${CACHE_VERSION}-fonts`;

const OFFLINE_URL   = '/offline.html';
const MAX_AGE_FONTS = 30 * 24 * 60 * 60; // 30 days

// Load the generated asset manifest into the service worker global scope.
// Page-level scripts do NOT populate the service worker execution context.
try {
    self.importScripts('./service-worker-assets.js');
} catch {
    // Continue without precache manifest; install still succeeds and
    // runtime caching strategies remain available.
}

/** Assets that must be pre-cached for the offline shell to load */
const PRECACHE_URLS = Array.isArray(self.__MLS_ASSETS__) ? self.__MLS_ASSETS__ : [];
if (PRECACHE_URLS.length === 0) {
    console.warn('[MLS SW] service-worker-assets.js not loaded or empty — precaching is disabled. ' +
        'Offline functionality will be limited to the "/" shell only.');
}

// ── Install ──────────────────────────────────────────────────────────────────
self.addEventListener('install', event => {
    event.waitUntil(
        (async () => {
            const shellCache = await caches.open(SHELL_CACHE);
            // Always cache the app shell and offline fallback
            await shellCache.addAll(['/', OFFLINE_URL]);

            // Pre-cache known assets from the asset manifest
            if (PRECACHE_URLS.length > 0) {
                const frameCache = await caches.open(FRAME_CACHE);
                await frameCache.addAll(
                    PRECACHE_URLS
                        .filter(a => a.url.includes('_framework'))
                        .map(a => a.url)
                );

                const assetCache = await caches.open(ASSET_CACHE);
                await assetCache.addAll(
                    PRECACHE_URLS
                        .filter(a => !a.url.includes('_framework') && !a.url.includes('api'))
                        .map(a => a.url)
                );
            }

            await self.skipWaiting();
        })()
    );
});

// ── Activate ─────────────────────────────────────────────────────────────────
self.addEventListener('activate', event => {
    event.waitUntil(
        (async () => {
            // Remove obsolete cache versions
            const knownCaches = new Set([SHELL_CACHE, FRAME_CACHE, ASSET_CACHE, FONT_CACHE]);
            const existingCaches = await caches.keys();
            await Promise.all(
                existingCaches
                    .filter(name => name.startsWith('mls-') && !knownCaches.has(name))
                    .map(name => caches.delete(name))
            );
            await self.clients.claim();
        })()
    );
});

// ── Fetch ─────────────────────────────────────────────────────────────────────
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // Skip non-GET, chrome-extension, and SignalR WS negotiate calls
    if (request.method !== 'GET') {
        event.respondWith(networkWithOrderQueue(request));
        return;
    }
    if (!url.protocol.startsWith('http')) return;
    if (url.pathname.includes('/hubs/')) return;
    if (url.pathname.endsWith('/negotiate')) return;
    if (url.pathname.includes('/_blazor')) return;

    // ── SPA navigation: serve cached shell, fall back to offline page ─────────
    if (request.mode === 'navigate') {
        event.respondWith(
            (async () => {
                try {
                    const network = await fetch(request);
                    return network;
                } catch {
                    const shellCache = await caches.open(SHELL_CACHE);
                    return (await shellCache.match('/')) ||
                           (await shellCache.match(OFFLINE_URL)) ||
                           new Response('Offline', { status: 503 });
                }
            })()
        );
        return;
    }

    // ── API: NetworkFirst ─────────────────────────────────────────────────────
    if (url.pathname.startsWith('/api/')) {
        event.respondWith(networkFirst(request, ASSET_CACHE, 5000));
        return;
    }

    // ── Blazor framework files: CacheFirst (immutable) ────────────────────────
    if (url.pathname.includes('/_framework/') || url.hostname === 'cdn.jsdelivr.net') {
        event.respondWith(cacheFirst(request, FRAME_CACHE));
        return;
    }

    // ── Fonts (Google Fonts, etc): CacheFirst 30-day expiry ───────────────────
    if (url.hostname === 'fonts.googleapis.com' || url.hostname === 'fonts.gstatic.com') {
        event.respondWith(cacheFirstWithExpiry(request, FONT_CACHE, MAX_AGE_FONTS));
        return;
    }

    // ── Icons + static assets: CacheFirst ─────────────────────────────────────
    if (
        url.pathname.startsWith('/icons/') ||
        url.pathname.startsWith('/css/') ||
        url.pathname.startsWith('/js/') ||
        /\.(png|svg|ico|woff2?|ttf|otf|webp|jpg|jpeg)$/.test(url.pathname)
    ) {
        event.respondWith(cacheFirst(request, ASSET_CACHE));
        return;
    }

    // ── Shell HTML: StaleWhileRevalidate ──────────────────────────────────────
    event.respondWith(staleWhileRevalidate(request, SHELL_CACHE));
});

// ── Background Sync: order queue ─────────────────────────────────────────────
self.addEventListener('sync', event => {
    if (event.tag === 'mls-order-queue') {
        event.waitUntil(replayOrderQueue());
    }
});

// ── Push notifications ────────────────────────────────────────────────────────
self.addEventListener('push', event => {
    if (!event.data) return;
    const data = event.data.json();
    event.waitUntil(
        self.registration.showNotification(data.title || 'MLS Alert', {
            body: data.body || '',
            icon: '/icons/icon-192.svg',
            badge: '/icons/icon-192.svg',
            tag: data.tag || 'mls-notification',
            data: data.url ? { url: data.url } : undefined
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    if (event.notification.data?.url) {
        event.waitUntil(clients.openWindow(event.notification.data.url));
    }
});

// ── Helpers ───────────────────────────────────────────────────────────────────

async function cacheFirst(request, cacheName) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);
    if (cached) return cached;
    try {
        const response = await fetch(request);
        if (response.ok) cache.put(request, response.clone());
        return response;
    } catch {
        return new Response(
            JSON.stringify({ error: 'offline', cached: false }),
            { status: 503, headers: { 'Content-Type': 'application/json' } }
        );
    }
}

async function cacheFirstWithExpiry(request, cacheName, maxAgeSeconds) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);
    if (cached) {
        const cachedDate = new Date(cached.headers.get('date') || 0);
        const age = (Date.now() - cachedDate.getTime()) / 1000;
        if (age < maxAgeSeconds) return cached;
    }
    try {
        const response = await fetch(request);
        if (response.ok) cache.put(request, response.clone());
        return response;
    } catch {
        return cached || new Response('Offline', { status: 503 });
    }
}

async function networkFirst(request, cacheName, timeoutMs) {
    const cache = await caches.open(cacheName);
    try {
        const networkPromise = fetch(request);
        const timeoutPromise = new Promise((_, reject) =>
            setTimeout(() => reject(new Error('timeout')), timeoutMs)
        );
        const response = await Promise.race([networkPromise, timeoutPromise]);
        if (response.ok) cache.put(request, response.clone());
        return response;
    } catch {
        const cached = await cache.match(request);
        return cached || new Response(
            JSON.stringify({ error: 'offline', cached: false }),
            { status: 503, headers: { 'Content-Type': 'application/json' } }
        );
    }
}

async function staleWhileRevalidate(request, cacheName) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);
    const networkFetch = fetch(request).then(response => {
        if (response.ok) cache.put(request, response.clone());
        return response;
    }).catch(() => null);
    return cached || await networkFetch || new Response('Offline', { status: 503 });
}

async function networkWithOrderQueue(request) {
    try {
        return await fetch(request);
    } catch {
        // POST to /api/orders → queue for background sync
        if (request.url.includes('/api/orders') || request.url.includes('/api/trade')) {
            await queueOrder(request.clone());
            return new Response(
                JSON.stringify({ queued: true, message: 'Order queued for sync when online' }),
                { status: 202, headers: { 'Content-Type': 'application/json' } }
            );
        }
        throw new Error('Network request failed');
    }
}

const ORDER_STORE = 'mls-order-queue';

async function queueOrder(request) {
    const body = await request.text();
    const db = await openOrderDb();
    const tx = db.transaction(ORDER_STORE, 'readwrite');
    tx.objectStore(ORDER_STORE).add({
        url: request.url,
        method: request.method,
        headers: Object.fromEntries(request.headers.entries()),
        body,
        timestamp: Date.now()
    });
    await txComplete(tx);
    db.close();
    // Register for background sync when available
    if ('sync' in self.registration) {
        await self.registration.sync.register('mls-order-queue');
    }
}

async function replayOrderQueue() {
    const db = await openOrderDb();
    const tx = db.transaction(ORDER_STORE, 'readwrite');
    const store = tx.objectStore(ORDER_STORE);
    const orders = await storeGetAll(store);

    await Promise.allSettled(
        orders.map(async order => {
            try {
                const response = await fetch(order.url, {
                    method: order.method,
                    headers: order.headers,
                    body: order.body
                });
                if (response.ok) {
                    const delTx = db.transaction(ORDER_STORE, 'readwrite');
                    delTx.objectStore(ORDER_STORE).delete(order.id);
                    await txComplete(delTx);
                }
            } catch { /* leave in queue for next sync */ }
        })
    );
    db.close();
}

function openOrderDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open('mls-sw-db', 1);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            if (!db.objectStoreNames.contains(ORDER_STORE)) {
                db.createObjectStore(ORDER_STORE, { keyPath: 'id', autoIncrement: true });
            }
        };
        req.onsuccess = e => resolve(e.target.result);
        req.onerror = e => reject(e.target.error);
    });
}

function storeGetAll(store) {
    return new Promise((resolve, reject) => {
        const req = store.getAll();
        req.onsuccess = e => resolve(e.target.result);
        req.onerror = e => reject(e.target.error);
    });
}

function txComplete(tx) {
    return new Promise((resolve, reject) => {
        tx.oncomplete = resolve;
        tx.onerror = e => reject(e.target.error);
    });
}
