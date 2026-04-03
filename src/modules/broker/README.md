# broker — Broker Integration (HYPERLIQUID)

> **Status**: 🔧 Scaffold — project not yet implemented.

## Overview

The `broker` module integrates with **HYPERLIQUID** as the primary DEX/perpetuals broker.
It handles order placement, order tracking, position management, and fill notifications.
All venue addresses and API endpoints are loaded from the `blockchain_addresses` PostgreSQL table —
never hardcoded.

## Ports

| Protocol | Port |
|----------|------|
| HTTP API | 5800 |
| WebSocket | 6800 |

## Structure (planned)

```
src/modules/broker/
└── Broker/
    ├── Broker.csproj
    ├── Program.cs
    ├── Services/
    │   ├── HyperliquidBrokerService.cs
    │   └── OrderTrackingService.cs
    ├── Hubs/
    └── Dockerfile
```

## Dependencies

- `block-controller` (event routing)
- `transactions` (signed transaction submission)
- PostgreSQL `blockchain_addresses` table (all venue addresses)

See [SESSION.md](docs/SESSION.md) for Copilot session prompt.
