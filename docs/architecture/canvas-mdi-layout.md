> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Canvas MDI Layout — Web App Architecture

> **Reference**: [Giga-Scale Plan](giga-scale-plan.md) | [Session Schedule](../session-schedule.md) (Sessions 11–14, 17–18)

---

## MDI Window Manager Architecture

```
CanvasHost.razor
└── WindowContainer (position: relative; overflow: hidden; 100vw × 100vh)
    └── DocumentWindow[] (position: absolute; z-order managed by WindowManager)
        ├── TitleBar
        │   ├── Icon + Panel title
        │   ├── Drag handle (mousedown → WindowManager.StartDrag)
        │   ├── Minimize button → WindowManager.Minimize(windowId)
        │   ├── Maximize button → WindowManager.Maximize(windowId)
        │   ├── Close button   → WindowManager.Close(windowId)
        │   └── Detach button  → WindowManager.Detach(windowId) [popout]
        ├── ResizeHandles (8 directions: N, NE, E, SE, S, SW, W, NW)
        └── ContentSlot (renders any panel component as @ChildContent)
```

### WindowManager Service

```csharp
/// <summary>MDI layout state — tracks all open DocumentWindows.</summary>
public sealed class WindowManager(
    IWindowLayoutService _layoutService,
    ILogger<WindowManager> _logger
)
{
    private readonly ConcurrentDictionary<Guid, WindowState> _windows = new();

    public Guid OpenPanel(string panelType, object? data = null, string? title = null) { }
    public void Close(Guid windowId) { }
    public void Minimize(Guid windowId) { }
    public void Maximize(Guid windowId) { }
    public void BringToFront(Guid windowId) { }
    public void StartDrag(Guid windowId, double startX, double startY) { }
    public void ContinueDrag(double x, double y) { }
    public void EndDrag() { }
    public void StartResize(Guid windowId, ResizeDirection direction) { }

    /// Persist layout to localStorage via IWindowLayoutService
    public Task SaveLayoutAsync(CancellationToken ct) { }

    /// Restore layout from localStorage on page load
    public Task RestoreLayoutAsync(CancellationToken ct) { }
}

public sealed record WindowState(
    Guid WindowId,
    string PanelType,
    string Title,
    double X, double Y,
    double Width, double Height,
    int ZIndex,
    bool IsMinimized,
    bool IsMaximized
);
```

---

## MDI Panel Catalog

| Panel Type | Component | Opens From | Description |
|------------|-----------|------------|-------------|
| `TradingChart` | `TradingChart.razor` | AI chat, toolbar, AR scanner | OHLCV candlestick + indicator overlays + signal markers |
| `TradingTerminal` | `TradingTerminal.razor` | Main nav | Full trading terminal with order book + positions |
| `OrderBook` | `OrderBook.razor` | TradingTerminal, toolbar | Real-time L2 depth heatmap |
| `PositionsGrid` | `PositionsGrid.razor` | TradingTerminal, AI chat | Open positions with live P&L + close buttons |
| `ArbitrageScanner` | `ArbitrageScanner.razor` | Main nav | Live nHOP opportunity table |
| `PathVisualization` | `PathVisualization.razor` | ArbitrageScanner | Cytoscape.js directed token graph with live edge weights |
| `DeFiPositions` | `DeFiPositions.razor` | Main nav | Supply/borrow positions with health factors |
| `HealthMonitor` | `HealthMonitor.razor` | DeFiPositions | Health factor gauge + critical alert threshold |
| `DesignerCanvas` | `DesignerCanvas.razor` | Main nav, AI chat | Block graph editor (SVG) + palette + property editor |
| `ModelRegistry` | `ModelRegistry.razor` | ML nav | All models, versions, lineage, metrics |
| `InferenceMetrics` | `InferenceMetrics.razor` | ML nav | Latency histogram, throughput, error rate |
| `TrainProgress` | `TrainProgress.razor` | AI chat, ML nav | Live training: loss curve, accuracy, confusion matrix |
| `NetworkTopology` | `NetworkTopology.razor` | Observatory | Cytoscape.js module graph with WS edge status |
| `ModuleCard` | `ModuleCard.razor` | Observatory | Health card: uptime, CPU, memory, last heartbeat |
| `EnvelopeStream` | `EnvelopeStream.razor` | Observatory, debug | Live filtered envelope viewer with regex search |
| `DataFeedManager` | `DataFeedManager.razor` | Data nav | Active feeds, latency, last candle, gap count |
| `ShellTerminal` | `TerminalBlock.razor` | Shell nav, AI chat | xterm.js Shell VM terminal |
| `AIChatPanel` | `AIChatPanel.razor` | AI FAB, sidebar | Streaming AI chat with canvas actions |
| `PnLReport` | `PnLReport.razor` | AI chat, Reports nav | P&L summary with equity curve chart |
| `ProviderSettings` | `ProviderSettings.razor` | Settings | LLM provider configuration + fallback chain |

---

## Component Hierarchy

```
MainLayout.razor
├── FluentDesignTheme (Mode="Dark")
├── FluentNavMenu (responsive: full / collapsed / hamburger)
│   ├── NavItems: Dashboard, Trading, Arbitrage, DeFi, ML, Designer, Data, Shell, AI
│   └── Module health indicators (green/amber/red dots per module)
│
├── CanvasHost.razor
│   ├── WindowContainer
│   │   └── DocumentWindow × N (from WindowManager state)
│   ├── WindowTaskbar (bottom: minimized windows list)
│   └── QuickOpenFAB (floating button: opens most-used panels)
│
├── FluentToastProvider
├── FluentDialogProvider
├── FluentMessageBarProvider
└── FluentTooltipProvider
```

---

## Real-Time Update Pattern

All panels follow this pattern to avoid unnecessary Blazor re-renders:

```csharp
// ❌ Wrong: triggers full component re-render on every message
protected override async Task OnInitializedAsync()
{
    await foreach (var envelope in _hub.GetEnvelopeStreamAsync(topics, _ct))
    {
        _data = envelope.Payload;
        StateHasChanged();   // full re-render
    }
}

// ✅ Correct: targeted JS DOM update, no Blazor render cycle
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    await foreach (var envelope in _hub.GetEnvelopeStreamAsync(topics, _ct))
    {
        // Update only the changed field
        await JS.InvokeVoidAsync("updatePositionRow", envelope.Symbol, envelope.Payload);
    }
}
```

### JS Interop Update Functions

```javascript
// canvas-updates.js — targeted DOM updates for all real-time panels
window.updatePositionRow = (symbol, data) => { /* update table row */ }
window.updateApexSeries = (chartId, seriesName, epoch, value) => { /* push data point */ }
window.updateOrderBook = (symbol, bids, asks) => { /* redraw heatmap */ }
window.pushCandleToChart = (chartId, candle) => { /* append OHLCV candle */ }
window.addTradeAnnotation = (chartId, signal) => { /* signal marker on chart */ }
window.updateHealthFactor = (gaugeId, value) => { /* animate gauge */ }
window.renderMermaidDiagram = (containerId, source) => { /* mermaid.render */ }
```

---

## Responsive Breakpoint System

### Viewport Mode Table

| Viewport | Mode | Layout | Nav | Panels |
|----------|------|--------|-----|--------|
| > 1440px | **Desktop Full MDI** | Floating windows, multi-panel | Full sidebar | Unrestricted, min 200×150 |
| 1024–1440px | **Laptop Docked** | Split panels, no floating | Collapsed icons | Side-by-side, 2 panels max |
| 768–1024px | **Tablet** | Single active + bottom sheet | Icon bar | One foreground + sheet select |
| < 768px | **Mobile** | Single panel stack | Hamburger | Stack navigation, FAB |

### CSS Container Queries

```css
/* CanvasHost responsive behaviour */
@container canvas (max-width: 768px) {
    .document-window {
        position: relative !important;
        width: 100% !important;
        height: auto !important;
        transform: none !important;
    }
    .window-titlebar .resize-handle { display: none; }
    .window-titlebar .btn-detach { display: none; }
}

@container canvas (max-width: 1024px) {
    .document-window {
        position: relative;
        float: left;
        min-width: 45%;
    }
}
```

### FluentUI Responsive Nav

```razor
@* MainLayout.razor — auto-collapse nav on mobile *@
<FluentNavMenu Width="250" Collapsible="true" Collapsed="@_navCollapsed">
    @* items *@
</FluentNavMenu>

@code {
    private bool _navCollapsed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var width = await JS.InvokeAsync<double>("window.innerWidth");
        _navCollapsed = width < 1024;
    }
}
```

---

## DesignerCanvas Component

### SVG Block Graph Architecture

```
DesignerCanvas.razor
├── SVG Element (full canvas, pan/zoom via transform matrix)
│   ├── ConnectionsLayer (SVG <path> bezier curves per connection)
│   │   └── Animated pulse circles on BLOCK_SIGNAL (Phase 7)
│   ├── BlocksLayer (SVG <foreignObject> per block for DOM content)
│   │   └── BlockNode.razor (type badge, socket indicators, parameter summary)
│   └── DragGhostLayer (follows mouse during block drag)
│
├── BlockPalette.razor (FluentDrawer, slides from left)
│   └── Categorized accordion: Trading / Arbitrage / DeFi / ML / Data
│
├── PropertyEditor.razor (FluentDrawer, slides from right on block select)
│   └── Typed parameter inputs: int, float, string, enum, bool
│
└── DesignerToolbar (top: deploy, stop, backtest, undo, redo, zoom fit)
```

### Touch + Mouse Interaction (canvas-interop.js)

```javascript
// HammerJS: pinch-to-zoom, pan gesture for touch devices
// Mouse: wheel-to-zoom, middle-click drag to pan
// Block drag: mousedown on block → move SVG transform → mouseup to snap
// Connection: mousedown on output socket → draw rubber-band line → release on input socket
```

---

## PWA Manifest

```json
{
  "name": "MLS Trading Platform",
  "short_name": "MLS",
  "description": "Machine Learning Studio — Trading, Arbitrage, and DeFi Platform",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#0d1117",
  "theme_color": "#00d4ff",
  "orientation": "any",
  "categories": ["finance", "productivity"],
  "icons": [
    { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icons/icon-512-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ],
  "shortcuts": [
    { "name": "Trading Terminal", "url": "/?panel=trading", "icons": [{ "src": "/icons/shortcut-trading.png" }] },
    { "name": "AI Chat", "url": "/?panel=ai-chat", "icons": [{ "src": "/icons/shortcut-ai.png" }] }
  ]
}
```

## Service Worker Strategy

```javascript
// service-worker.js (Workbox-based)
import { registerRoute } from 'workbox-routing';
import { CacheFirst, NetworkFirst, StaleWhileRevalidate } from 'workbox-strategies';
import { BackgroundSyncPlugin } from 'workbox-background-sync';

// Blazor framework: immutable, cached forever
registerRoute(/_framework\//, new CacheFirst({ cacheName: 'blazor-framework' }));

// API calls: always network-first (trading data must be live)
registerRoute(/\/api\//, new NetworkFirst({ cacheName: 'api-cache', networkTimeoutSeconds: 3 }));

// WebSocket connections: not interceptable by SW — handled by Blazor SignalR reconnect logic

// Queued order sync when offline → replay on reconnect
const orderSyncPlugin = new BackgroundSyncPlugin('order-queue', { maxRetentionTime: 60 });
registerRoute(/\/api\/orders/, new NetworkFirst({ plugins: [orderSyncPlugin] }), 'POST');

// App shell: stale while revalidate
registerRoute(/\/$/, new StaleWhileRevalidate({ cacheName: 'app-shell' }));
```

---

## Chrome Extension (MV3)

### manifest.v3.json

```json
{
  "manifest_version": 3,
  "name": "MLS Trading Platform",
  "version": "1.0.0",
  "description": "MLS platform sidebar and overlay for Chrome",
  "permissions": ["storage", "tabs", "sidePanel", "alarms"],
  "host_permissions": ["http://localhost:5200/*", "ws://localhost:6200/*"],
  "background": { "service_worker": "background.js", "type": "module" },
  "content_scripts": [{
    "matches": ["*://app.hyperliquid.xyz/*", "*://app.camelot.exchange/*"],
    "js": ["content.js"]
  }],
  "side_panel": { "default_path": "sidebar/index.html" },
  "action": { "default_popup": "popup/index.html", "default_icon": { "128": "icons/icon-128.png" } }
}
```

### Side Panel Components

```
sidebar/
├── index.html          → loads web-app WASM in iframe (same origin: localhost:5200)
├── SidebarApp.razor    → mini MDI with key panels only
│   ├── MiniChart       (TradingChart, symbol picker)
│   ├── PositionsSummary (top 5 positions)
│   ├── ArbOpportunities (top 3 live opportunities)
│   └── MiniAIChat      (full AI chat with canvas actions)
└── background.js       → chrome.runtime.connect → MLS WebSocket proxy
```

### Content Script Overlay

```javascript
// content.js — injects MLS price overlay on supported exchange pages
// Supported: Hyperliquid, Camelot exchange interfaces
// Overlay: floating widget, draggable, shows:
//   - Current MLS signal for active symbol (BUY/SELL/HOLD + confidence)
//   - Position health indicator
//   - Quick-access AI chat button
```

---

## Motion Design System

All animations follow FluentUI motion principles:

```css
/* Panel entrance */
@keyframes panel-enter {
    from { transform: translateY(20px); opacity: 0; }
    to   { transform: translateY(0);    opacity: 1; }
}

/* Data value flash on update */
@keyframes value-flash {
    0%   { background-color: rgba(0, 212, 255, 0.3); }
    100% { background-color: transparent; }
}

/* Connection pulse (designer canvas) */
@keyframes socket-pulse {
    0%   { transform: scale(1);   opacity: 1; }
    50%  { transform: scale(1.5); opacity: 0.7; }
    100% { transform: scale(1);   opacity: 1; }
}

/* Only animate transform + opacity (no layout reflow) */
/* All animations wrapped in: */
@media (prefers-reduced-motion: no-preference) {
    .document-window { animation: panel-enter 200ms ease-out; }
    .data-value.changed { animation: value-flash 400ms ease-out; }
}
```

---

## See Also

- [Session Schedule — Sessions 11–14, 17–18](../session-schedule.md#phase-3--mdi-canvas-rewrite)
- [AI Hub Providers](ai-hub-providers.md) — how AI canvas actions reach the MDI
- [Designer Block Graph](designer-block-graph.md) — block types rendered in DesignerCanvas
