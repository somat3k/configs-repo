# web-app — Session 12: Trading + Arbitrage + DeFi Panels

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 12. Trading + Arbitrage + DeFi Panels

**Phase**: 3 — MDI Canvas Rewrite

**Objective**: Implement all domain-specific MDI panels: TradingTerminal, ArbitrageScanner, PathVisualization, DeFiPositions.

---

### Files Created

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/Trading/TradingTerminal.razor` | Chart + order book + positions |
| CREATE | `src/web-app/WebApp/Components/Trading/TradingChart.razor` | ApexCharts candlestick + signal overlays |
| CREATE | `src/web-app/WebApp/Components/Trading/OrderBook.razor` | Real-time L2 depth display |
| CREATE | `src/web-app/WebApp/Components/Trading/PositionsGrid.razor` | Open positions with live P&L |
| CREATE | `src/web-app/WebApp/Components/Arbitrage/ArbitrageScanner.razor` | Live opportunity table |
| CREATE | `src/web-app/WebApp/Components/Arbitrage/PathVisualization.razor` | Cytoscape.js token graph |
| CREATE | `src/web-app/WebApp/Components/DeFi/DeFiPositions.razor` | Supply/borrow positions |
| CREATE | `src/web-app/WebApp/Components/DeFi/HealthMonitor.razor` | Health factor gauge + alert |

---

### Real-Time Update Pattern (all panels)

All domain panels share the same non-re-render pattern: subscribe on `OnAfterRenderAsync(firstRender)`, update targeted DOM elements via JS interop, and only call `StateHasChanged` when structural DOM changes are required.

```csharp
// No full component re-renders on data updates
// Use SignalR → JS interop → targeted DOM update
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    await foreach (var envelope in Hub
        .GetEnvelopeStreamAsync(["TRADE_SIGNAL", "POSITION_UPDATE"], _cts.Token)
        .ConfigureAwait(false))
    {
        // Update only changed data fields — no StateHasChanged for full render
        var symbol = envelope.Payload.GetProperty("symbol").GetString()!;
        await JS.InvokeVoidAsync("updatePositionRow", symbol, envelope.Payload)
            .ConfigureAwait(false);
    }
}
```

---

### Panel Details

#### TradingTerminal
- Composite host containing `TradingChart`, `OrderBook`, and `PositionsGrid` in a split layout.
- Symbol and interval selectors drive all child panels via cascading parameters.

#### TradingChart
- ApexCharts `candlestick` series rendered via `JS.InvokeVoidAsync("initApexChart", ...)`.
- Trade signal overlays injected via `JS.InvokeVoidAsync("addApexAnnotation", ...)` on each `TRADE_SIGNAL` envelope — no full chart re-initialisation.
- Subscribes to: `TRADE_SIGNAL`, `OHLCV_UPDATE`.

#### OrderBook
- L2 depth display: bids (green) / asks (red) in descending/ascending price order.
- Targeted row updates: `JS.InvokeVoidAsync("updateOrderBookRow", side, price, qty)`.
- Subscribes to: `ORDER_BOOK_UPDATE`.

#### PositionsGrid
- Fluent DataGrid with live P&L column.
- P&L colour transition driven by CSS class toggle (positive/negative) without full grid re-render.
- Subscribes to: `POSITION_UPDATE`, `TRADE_SIGNAL`.

#### ArbitrageScanner
- Table of live nHOP opportunities with min-profit filter.
- Rows colour-tiered: gold ≥ 0.5 %, green ≥ 0.1 %, default otherwise.
- Subscribes to: `ARB_PATH_FOUND`.

#### PathVisualization
- Cytoscape.js graph rendered via JS interop: token nodes, exchange edges with weight labels.
- `JS.InvokeVoidAsync("initCytoscapeGraph", containerId, elements)` on first render.
- `JS.InvokeVoidAsync("updateCytoscapeEdge", ...)` for live weight updates.
- Subscribes to: `ARB_PATH_FOUND`.

#### DeFiPositions
- Supply/borrow position grid per protocol (Morpho, Balancer).
- Utilisation bar updated via targeted DOM update.
- Subscribes to: `DEFI_POSITION_UPDATE`, `HEALTH_FACTOR_UPDATE`.

#### HealthMonitor
- SVG half-circle gauge with animated arc for health factor (0–3+).
- Animated pulsing alert banner when HF < 1.2; warning banner when HF < 1.5.
- Sparkline history (30 readings) drawn as SVG polyline.
- Subscribes to: `HEALTH_FACTOR_UPDATE`, `DEFI_HEALTH_WARNING`.

---

### Skills Applied

- `.skills/premium-uiux-blazor.md`
- `.skills/web-apps.md`
- `.skills/web3.md`

---

### Acceptance Criteria

- [x] `TradingChart` updates in real-time via SignalR without full re-render
- [x] `ArbitrageScanner` shows live nHOP paths with profit/gas breakdown
- [x] `PathVisualization` renders Cytoscape graph with token nodes and edge weights
- [x] `HealthMonitor` triggers animated alert when health factor < 1.2
- [x] All panels open as `DocumentWindow` instances from the CanvasHost

**Session Status: ✅ COMPLETE**

---
