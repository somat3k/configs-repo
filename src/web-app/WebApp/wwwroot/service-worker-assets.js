/**
 * MLS Trading Platform — Service Worker Asset Manifest
 * This file is auto-generated at build time by the PWA build step.
 * During development it lists the known static assets for offline pre-caching.
 *
 * Format: { url: string, revision: string | null }
 * - url:      relative URL from app root
 * - revision: content hash (null = versioned by URL itself, e.g. Blazor framework files)
 */

self.__MLS_ASSETS__ = [
    // ── App shell ──────────────────────────────────────────────────────────
    { url: '/',                           revision: 'shell-v1' },
    { url: '/css/app.css',                revision: 'css-v1' },
    { url: '/css/responsive.css',         revision: 'responsive-v1' },
    { url: '/manifest.json',              revision: 'manifest-v1' },

    // ── Icons ───────────────────────────────────────────────────────────────
    { url: '/icons/icon-192.svg',         revision: 'icon-v1' },
    { url: '/icons/icon-512.svg',         revision: 'icon-v1' },
    { url: '/icons/icon-maskable-192.svg',revision: 'icon-v1' },
    { url: '/icons/icon-maskable-512.svg',revision: 'icon-v1' },

    // ── JS interop ──────────────────────────────────────────────────────────
    { url: '/js/canvas-interop.js',       revision: 'interop-v1' },

    // ── Blazor WASM bootstrap (versioned by hash — no revision needed) ─────
    { url: '/_framework/blazor.web.js',   revision: null },
];
