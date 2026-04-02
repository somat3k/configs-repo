---
title: MLS — Machine Learning Studio
description: Enterprise-grade distributed platform for algorithmic trading, on-chain arbitrage, and DeFi services.
---

# Machine Learning Studio

> Enterprise-grade distributed platform for **algorithmic trading**, **on-chain arbitrage**, and **DeFi services**.

## Platform Overview

The MLS platform is a production-ready distributed system built with:
- **C# .NET 9** — backend modules and inference engine
- **Blazor** — interactive web MDI interface
- **Python 3.12** — ML model training and export
- **ONNX Runtime** — production ML inference (< 10ms)
- **PostgreSQL** — primary relational database
- **Redis** — real-time cache layer
- **IPFS** — distributed model and data storage
- **HYPERLIQUID** — primary DEX/perpetuals broker (no Uniswap)

## Quick Links

- [Architecture](/architecture) — Module topology and system design
- [Modules](/modules) — Per-module documentation
- [API Reference](/api) — WebSocket payload schemas
- [Checklist](/checklist) — Development progress tracker
- [GitHub Repository](https://github.com/somat3k/configs-repo)

## Module Map

| Module | Role | Port |
|--------|------|------|
| Block Controller | Orchestration hub | 5100/6100 |
| Web App | Blazor MDI UI | 5200 |
| Trader | Algo trading | 5300/6300 |
| Arbitrager | Arbitrage model | 5400/6400 |
| DeFi | DeFi services | 5500/6500 |
| ML Runtime | Inference engine | 5600/6600 |
| Data Layer | Data access | 5700/6700 |
| Broker | Exchange integration | 5800/6800 |
| Transactions | Transaction manager | 5900/6900 |
