# 🏦 Machine Learning Studio — Trading, Arbitrage & DeFi

> Enterprise-grade distributed platform for algorithmic trading, on-chain arbitrage, and DeFi services, powered by machine learning.

[![CI](https://github.com/somat3k/configs-repo/actions/workflows/ci.yml/badge.svg)](https://github.com/somat3k/configs-repo/actions/workflows/ci.yml)
[![Pages](https://github.com/somat3k/configs-repo/actions/workflows/pages.yml/badge.svg)](https://somat3k.github.io/configs-repo)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## 🏗️ Architecture Overview

```
┌─────────────────── Web App (Blazor MDI) ──────────────────────┐
│   Trader │ Arbitrager │ DeFi │ Network │ Observatory │ Config  │
└───────────────────────┬───────────────────────────────────────┘
                        │ SignalR/WebSocket
          ┌─────────────▼──────────────┐
          │     Block Controller       │  ← Orchestration Hub
          │      (port 5100/6100)      │
          └──────┬────────┬────────────┘
      ┌──────────┤        └────────────────┐
      │          │                         │
 ┌────▼───┐ ┌───▼────┐ ┌───────┐ ┌───────▼──────┐
 │ Trader │ │  Arb   │ │ DeFi  │ │  ML Runtime  │
 │  5300  │ │  5400  │ │ 5500  │ │   5600/6600  │
 └────────┘ └────────┘ └───────┘ └──────────────┘
          │          │         │
     ┌────▼──────────▼─────────▼────┐
     │        Data-Driven Layer     │
     │  PostgreSQL │ Redis │  IPFS  │
     └──────────────────────────────┘
```

## 📦 Modules

| Module | Port (HTTP/WS) | Role | Status |
|--------|----------------|------|--------|
| [block-controller](src/block-controller/README.md) | 5100/6100 | Orchestration hub | 🔧 Scaffold |
| [web-app](src/web-app/README.md) | 5200/6200 | Blazor MDI UI | 🔧 Scaffold |
| [trader](src/modules/trader/README.md) | 5300/6300 | Algo trading model | 🔧 Scaffold |
| [arbitrager](src/modules/arbitrager/README.md) | 5400/6400 | Arbitrage model | 🔧 Scaffold |
| [defi](src/modules/defi/README.md) | 5500/6500 | DeFi services | 🔧 Scaffold |
| [ml-runtime](src/modules/ml-runtime/README.md) | 5600/6600 | ML training & inference | 🔧 Scaffold |
| [data-layer](src/modules/data-layer/README.md) | 5700/6700 | Data access layer | 🔧 Scaffold |
| [broker](src/modules/broker/README.md) | 5800/6800 | Broker integration (HYPERLIQUID) | 🔧 Scaffold |
| [transactions](src/modules/transactions/README.md) | 5900/6900 | Transaction manager | 🔧 Scaffold |
| [network-modules](src/network-modules/README.md) | — | ID gen, registry, runtime, VM | 🔧 Scaffold |

## 🚀 Quick Start

### Prerequisites
- .NET 9 SDK
- Docker Desktop
- Node.js 20+
- Python 3.12+

### 1. Start Infrastructure
```bash
docker compose -f docker-compose.infra.yml up -d
```

### 2. Start All Modules (Development)
```bash
# Using .NET Aspire (once src/MLS.AppHost is scaffolded)
# dotnet run --project src/MLS.AppHost

# Or individual modules via VS Code tasks
# Press Ctrl+Shift+P → "Tasks: Run Task" → "🚀 Full Stack (All Modules)"
```

### 3. Open Web App
Navigate to `https://localhost:7200`

### 4. Documentation Site (NuxtJS)
```bash
cd nuxt-pages && npm install && npm run dev
# Open http://localhost:3000
```

## 🧠 Skills (GitHub Copilot)

Skills are in `.skills/` — they guide Copilot code generation for this project:

| Skill | Description |
|-------|-------------|
| [dotnet-devs](.skills/dotnet-devs.md) | C#/.NET best practices |
| [web-apps](.skills/web-apps.md) | ASP.NET Core / Blazor patterns |
| [premium-uiux-blazor](.skills/premium-uiux-blazor.md) | FluentUI Blazor, MDI canvas, charts |
| [system-architect](.skills/system-architect.md) | Module topology, envelope protocol |
| [web3](.skills/web3.md) | HYPERLIQUID, DeFi, on-chain transactions |
| [machine-learning](.skills/machine-learning.md) | ONNX, JOBLIB, neural networks |
| [python](.skills/python.md) | ML training scripts, pipelines |
| [networking](.skills/networking.md) | .NET Aspire, WebSocket mesh |
| [storage-data-management](.skills/storage-data-management.md) | PostgreSQL, Redis, IPFS |
| [websockets-inferences](.skills/websockets-inferences.md) | SignalR, streaming, payload schemas |
| [beast-development](.skills/beast-development.md) | High-performance, low-latency patterns |
| [artificial-intelligence](.skills/artificial-intelligence.md) | Semantic Kernel, ONNX inference |
| [agents](.skills/agents.md) | Module agents, orchestration |

## 📋 Checklists

- [CHECKLIST.md](CHECKLIST.md) — Extensive test, debug, and development checklist

## 🗂️ Repository Structure

```
configs-repo/
├── .github/
│   ├── copilot-instructions.md     # Copilot project instructions
│   ├── workflows/                  # CI/CD pipelines
│   └── copilot-rules/              # Domain-specific copilot rules
├── .skills/                        # GitHub Copilot skills (from awesome-copilot)
├── .vscode/                        # VS Code settings, tasks, launch configs
├── src/
│   ├── MLS.AppHost/                # .NET Aspire orchestration
│   ├── MLS.Core/                   # Shared contracts and constants
│   ├── block-controller/           # Root orchestration module
│   ├── web-app/                    # Blazor web application
│   ├── modules/
│   │   ├── trader/                 # Trader algo-model
│   │   ├── arbitrager/             # Arbitrager algo-model
│   │   ├── defi/                   # DeFi services
│   │   ├── ml-runtime/             # ML training & inference (Python + C#)
│   │   ├── data-layer/             # Data-driven access layer
│   │   ├── broker/                 # Broker integration
│   │   └── transactions/           # Transaction management
│   └── network-modules/            # Network infrastructure modules
├── nuxt-pages/                     # GitHub Pages documentation (NuxtJS)
├── infra/                          # Infrastructure configs
│   ├── postgres/init/              # PostgreSQL init scripts
│   └── redis/                      # Redis config
├── docs/                           # Architecture documentation
├── artifacts/                      # ML model artifacts (gitignored)
├── docker-compose.yml              # Full platform
├── docker-compose.infra.yml        # Infrastructure only
└── MLS.sln                         # Solution file
```

## 🤝 Contributing

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for development guidelines.

## 📜 License

MIT License — see [LICENSE](LICENSE)
