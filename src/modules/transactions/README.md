# transactions — Transaction Manager

> **Status**: 🔧 Scaffold — project not yet implemented.

## Overview

The `transactions` module manages blockchain transaction lifecycle: construction, signing,
submission, monitoring, and confirmation. It provides a reliable queue for on-chain operations
with retry, nonce management, and receipt tracking.

## Ports

| Protocol | Port |
|----------|------|
| HTTP API | 5900 |
| WebSocket | 6900 |

## Structure (planned)

```
src/modules/transactions/
└── Transactions/
    ├── Transactions.csproj
    ├── Program.cs
    ├── Services/
    │   ├── TransactionQueueService.cs
    │   ├── SigningService.cs
    │   └── ReceiptMonitorService.cs
    ├── Hubs/
    └── Dockerfile
```

## Dependencies

- `block-controller` (event routing)
- `broker` (order execution triggers)
- PostgreSQL `blockchain_addresses` table (contract addresses)

See [SESSION.md](SESSION.md) for Copilot session prompt.
