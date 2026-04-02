# web-app — Session 3: Key Pages & Components

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 3. Key Pages & Components


```
Pages/
├── Dashboard.razor          # Module health observatory
├── Trading.razor            # Trader signals + order book
├── Arbitrage.razor          # Arbitrage opportunity scanner
├── DeFi.razor               # DeFi positions and strategies
├── MLRuntime.razor          # Model registry and inference metrics
├── DataLayer.razor          # Market data feed status
├── ExecutionConsole.razor   # Shell VM terminal (xterm.js embedded)
└── Settings.razor           # Platform configuration

Components/
├── ModuleCard.razor         # Health card for each registered module
├── TradingChart.razor       # OHLCV + signal overlay chart
├── OrderBook.razor          # Real-time order book display
├── PositionTable.razor      # Open positions grid
├── ArbitrageScanner.razor   # Live opportunity table with confidence
├── MetricsPanel.razor       # CPU/memory/throughput gauges
├── TerminalBlock.razor      # xterm.js wrapper for Shell VM sessions
└── EnvelopeViewer.razor     # Debug: raw envelope message stream
```

---
