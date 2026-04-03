---
name: pwa-chrome
source: custom (MLS Trading Platform)
description: 'PWA manifest, Workbox service worker, Chrome Extension MV3, and responsive layout patterns for the MLS Blazor web app.'
---

# PWA + Chrome Extension — MLS Trading Platform

> Apply this skill when working on: PWA configuration, service worker, Chrome extension, mobile-responsive layout, or cross-platform Blazor rendering.

---

## PWA Configuration Rules

### manifest.json Requirements

```json
{
  "name": "MLS Trading Platform",
  "short_name": "MLS",
  "start_url": "/",
  "display": "standalone",        // Required for app-like experience
  "background_color": "#0d1117",  // Must match body background
  "theme_color": "#00d4ff",       // MLS cyan accent
  "orientation": "any",
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icons/icon-512-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

**Rules:**
- `maskable` icon MUST have safe zone (content within inner 80% of canvas)
- `background_color` MUST match CSS `body { background-color }` (prevents flash on launch)
- `display: "standalone"` removes browser chrome (address bar, tabs)
- Icons MUST be provided at 192 and 512 (both required for installability)

### Blazor PWA Registration

```csharp
// Program.cs — register service worker
builder.Services.AddPWAService(options =>
{
    options.OfflineFallbackPage = "/offline";
});
```

```razor
@* App.razor or MainLayout.razor — PWA meta *@
<link rel="manifest" href="/manifest.json" />
<meta name="theme-color" content="#00d4ff" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
```

---

## Service Worker Strategy

```javascript
// service-worker.js — Workbox strategies per resource type

// Blazor framework files: immutable (hashed filenames), CacheFirst forever
registerRoute(/_framework\//, new CacheFirst({ cacheName: 'blazor-fw' }));

// API calls: NetworkFirst — trading data must never be stale
registerRoute(/\/api\//, new NetworkFirst({
    cacheName: 'api',
    networkTimeoutSeconds: 3,  // Fall back to cache after 3s
    fetchOptions: { credentials: 'include' }
}));

// WebSocket: NOT interceptable by SW — handled by SignalR auto-reconnect

// App shell HTML: StaleWhileRevalidate
registerRoute(/\/$/, new StaleWhileRevalidate({ cacheName: 'app-shell' }));

// Icons + fonts: CacheFirst with 30-day expiry
registerRoute(/\/(icons|fonts)\//, new CacheFirst({
    cacheName: 'static-assets',
    plugins: [new ExpirationPlugin({ maxAgeSeconds: 30 * 24 * 60 * 60 })]
}));

// Order submission when offline: Background Sync
const orderSync = new BackgroundSyncPlugin('order-queue', { maxRetentionTime: 60 });
registerRoute(/\/api\/orders/, new NetworkFirst({ plugins: [orderSync] }), 'POST');
```

---

## Chrome Extension MV3 Rules

### manifest.v3.json

```json
{
  "manifest_version": 3,
  "background": { "service_worker": "background.js", "type": "module" },
  "content_security_policy": {
    "extension_pages": "script-src 'self'; object-src 'self'"
  }
}
```

**MV3 Specific Rules:**
- Background page is a SERVICE WORKER in MV3 — NOT a persistent page
- No inline scripts, no `eval()` — strict CSP required
- Use `chrome.alarms` for periodic tasks (not `setInterval` — SW can be suspended)
- WebSocket connections: open in background SW, forward messages via `chrome.runtime.connect`
- Reconnect WebSocket on SW wake-up (use `chrome.alarms.create` to keep SW alive)

### Side Panel (Chrome 114+)

```javascript
// background.js — open Side Panel on extension icon click
chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true });

// Side Panel loads the Blazor WASM app in an iframe:
// <iframe src="http://localhost:5200/?mode=sidebar" />
// Sidebar mode: reduced MDI, shows MiniChart + PositionsSummary + ArbOpportunities + AIChatPanel
```

### Content Script Overlay

```javascript
// content.js — injected on Hyperliquid + Camelot pages
// Creates draggable floating widget showing:
//   - Current MLS signal for active symbol
//   - Health factor alert if DeFi position at risk
//   - Quick AI chat button (opens Side Panel)

// Communicate with background SW:
const port = chrome.runtime.connect({ name: 'content-mls' });
port.postMessage({ type: 'GET_SIGNAL', symbol: detectSymbolFromPage() });
port.onMessage.addListener(msg => { if (msg.type === 'SIGNAL') updateOverlay(msg.data); });
```

---

## Responsive Layout Rules

### Breakpoints

```css
/* Container queries on CanvasHost — not viewport queries */
/* This allows panels to respond independently of viewport size */

@container canvas (max-width: 768px)  { /* Mobile: single panel stack */ }
@container canvas (max-width: 1024px) { /* Tablet: 2-panel side-by-side */ }

/* FluentUI breakpoint variables */
:root {
  --mobile-max: 767px;
  --tablet-max: 1023px;
  --laptop-max: 1439px;
}
```

### Blazor Rendering Mode Selection

```csharp
// Component-level rendering mode decisions:
// Real-time data → InteractiveServer (SignalR latency < 50ms)
// Designer canvas (graph editing) → InteractiveWebAssembly (offline capable)
// Static content → StreamingSSR (instant first paint)

// In component: @rendermode InteractiveServer
// In component: @rendermode InteractiveWebAssembly
// In Program.cs: builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()
```

### Mobile Touch Patterns

```javascript
// canvas-interop.js — HammerJS for touch gestures
const hammer = new Hammer(canvasElement, { recognizers: [
    [Hammer.Pinch, { enable: true }],
    [Hammer.Pan, { direction: Hammer.DIRECTION_ALL }],
]});
hammer.on('pinch', e => zoom(e.scale));
hammer.on('pan', e => pan(e.deltaX, e.deltaY));

// virtualViewport API: detect software keyboard on mobile
window.visualViewport.addEventListener('resize', () => {
    const keyboardVisible = window.innerHeight - window.visualViewport.height > 100;
    if (keyboardVisible) adjustForKeyboard();
});
```

---

## Animation Rules (Motion Design)

```css
/* Only animate transform + opacity — no layout properties */
/* Always wrap in prefers-reduced-motion check */

@media (prefers-reduced-motion: no-preference) {
    .document-window {
        animation: panel-enter 200ms ease-out;
    }
    .data-value.changed {
        animation: value-flash 400ms ease-out;
    }
}

/* Never animate: width, height, top, left, padding, margin, border */
/* Use transform: translate() for position changes */
/* Use will-change: transform only during animation, remove after */
```

---

## Lighthouse / Performance Targets

| Metric | Target |
|--------|--------|
| PWA score | ≥ 90 |
| LCP (Largest Contentful Paint) | < 2.5s |
| FID / INP | < 200ms |
| CLS | < 0.1 |
| Service Worker: Blazor framework cached | 100% offline |
| Service Worker: API calls | NetworkFirst, 3s fallback |
