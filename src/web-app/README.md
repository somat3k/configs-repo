# web-app — Blazor MDI Web Application

> **Status**: 🔧 Scaffold — project not yet implemented.

## Overview

The `web-app` module is the Blazor Interactive Server + WASM front-end for the MLS platform.
It provides a multi-document interface (MDI) canvas with panels for trading, arbitrage, DeFi,
network monitoring, ML observability, and configuration.

## Ports

| Protocol | Port |
|----------|------|
| HTTP (Kestrel) | 5200 |
| WebSocket (SignalR) | 6200 |

## Structure (planned)

```
src/web-app/
└── WebApp/
    ├── WebApp.csproj        # Blazor project
    ├── Program.cs
    ├── Components/          # Blazor components (MDI panels)
    ├── Hubs/                # SignalR hubs
    └── Dockerfile
```

## Dependencies

- `block-controller` (SignalR connection for data/events)
- Microsoft FluentUI Blazor (UI component library)

See [SESSION.md](docs/SESSION.md) for Copilot session prompt.
