# broker — Broker Integration (HYPERLIQUID)

> **Status**: ✅ Implemented — `MLS.Broker` project live on HTTP 5800 / WS 6800.

## Overview

The `broker` module integrates with **HYPERLIQUID** as the primary DEX/perpetuals broker.
It handles order placement, order tracking, position management, and fill notifications.
All venue addresses and API endpoints are loaded from the `blockchain_addresses` PostgreSQL table —
never hardcoded.

## Ports

| Protocol | Port |
|----------|------|
| HTTP API | 5800 |
| WebSocket / SignalR hub | 6800 |

## Structure

```
src/modules/broker/
├── Dockerfile
├── MLS.Broker/
│   ├── MLS.Broker.csproj
│   ├── Program.cs
│   ├── GlobalUsings.cs
│   ├── InternalsVisible.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Configuration/
│   │   └── BrokerOptions.cs
│   ├── Controllers/
│   │   ├── OrderController.cs      — POST/DELETE/GET /api/orders
│   │   └── PositionController.cs   — GET/POST /api/positions
│   ├── Hubs/
│   │   └── BrokerHub.cs            — SignalR hub at /hubs/broker
│   ├── Interfaces/
│   │   ├── IHyperliquidClient.cs
│   │   ├── IBrokerFallbackChain.cs
│   │   └── IOrderTracker.cs
│   ├── Models/
│   │   └── BrokerModels.cs         — PlaceOrderRequest, OrderResult, FillNotification, …
│   ├── Persistence/
│   │   ├── BrokerDbContext.cs
│   │   ├── OrderEntity.cs
│   │   ├── PositionEntity.cs
│   │   └── OrderRepository.cs
│   └── Services/
│       ├── BlockControllerClient.cs    — heartbeat & registration
│       ├── BrokerFallbackChain.cs      — cascading venue fallback
│       ├── FillNotificationService.cs  — HYPERLIQUID WS fill consumer
│       ├── HyperliquidClient.cs        — REST + WS client
│       └── OrderTracker.cs             — PostgreSQL + Redis order state
└── MLS.Broker.Tests/
    ├── BrokerModelsTests.cs
    ├── BrokerOptionsTests.cs
    ├── BrokerFallbackChainTests.cs
    ├── MessageTypesTests.cs
    ├── OrderRepositoryTests.cs
    └── OrderTrackerTests.cs
```

## Key Message Types

| Direction | Type | Description |
|-----------|------|-------------|
| Receives | `ORDER_CREATE` | From Trader or DeFi — place new order |
| Receives | `ORDER_CANCEL` | Cancel order by clientOrderId |
| Sends | `ORDER_CONFIRMATION` | Order accepted by venue |
| Sends | `FILL_NOTIFICATION` | Order partially or fully filled |
| Sends | `POSITION_UPDATE` | Current position state |
| Sends | `MODULE_HEARTBEAT` | To Block Controller every 5 s |

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/orders` | Place order (idempotent via clientOrderId) |
| `DELETE` | `/api/orders/{clientOrderId}` | Cancel open order |
| `GET` | `/api/orders/{clientOrderId}` | Get order by client ID |
| `GET` | `/api/orders/open` | Stream all open/partial orders |
| `GET` | `/api/positions/{symbol}` | Get current position |
| `POST` | `/api/positions/{symbol}/refresh` | Refresh + broadcast POSITION_UPDATE |
| `GET` | `/health` | Liveness check |

## Dependencies

- `block-controller` (event routing, heartbeat)
- PostgreSQL `orders` + `positions` + `blockchain_addresses` tables
- Redis (order ID hot cache)
- HYPERLIQUID REST + WebSocket APIs

## Environment Variables

| Variable | Description |
|----------|-------------|
| `HYPERLIQUID_WALLET_ADDRESS` | EVM wallet address used for API queries — loaded at runtime |
| `Broker__PostgresConnectionString` | PostgreSQL connection string |
| `Broker__RedisConnectionString` | Redis connection string |
| `Broker__BlockControllerUrl` | Block Controller HTTP URL |

See [SESSION.md](docs/SESSION.md) for Copilot session prompt.
