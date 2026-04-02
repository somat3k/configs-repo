---
name: system-architect
source: github/awesome-copilot/skills/architecture-blueprint-generator
description: 'System architecture patterns for the MLS distributed network — microservices, event-driven design, module topology, payload schemas, and deployment architecture.'
---

# System Architecture — MLS Trading Platform

## Architecture Pattern
The MLS platform uses **Event-Driven Microservices** with a central **Block Controller** as the orchestration hub. Each module is a self-contained network node communicating via WebSocket payloads and HTTP APIs.

## Module Network Topology
```
                    ┌─────────────────────┐
                    │   Block Controller  │ ← Root orchestration hub
                    │   (port: 5100/6100) │
                    └─────────┬───────────┘
              ┌───────────────┼───────────────┐
              │               │               │
    ┌─────────▼──────┐ ┌──────▼──────┐ ┌─────▼───────┐
    │ Trader Module  │ │  Arbitrager │ │    DeFi     │
    │ (5300/6300)    │ │  (5400/6400)│ │ (5500/6500) │
    └────────────────┘ └─────────────┘ └─────────────┘
              │               │               │
    ┌─────────▼───────────────▼───────────────▼──────┐
    │              Data-Driven Layer                  │
    │   Redis Cache | PostgreSQL | IPFS Storage       │
    └─────────────────────────────────────────────────┘
```

## Envelope Protocol
All inter-module messages MUST use this envelope:
```json
{
  "type": "MODULE_EVENT_TYPE",
  "version": 1,
  "session_id": "uuid-v4",
  "module_id": "unique-module-id",
  "timestamp": "ISO8601",
  "payload": { ... }
}
```

## Port Allocation
| Module | HTTP | WebSocket |
|--------|------|-----------|
| block-controller | 5100 | 6100 |
| web-app | 5200 | 6200 |
| trader | 5300 | 6300 |
| arbitrager | 5400 | 6400 |
| defi | 5500 | 6500 |
| ml-runtime | 5600 | 6600 |
| data-layer | 5700 | 6700 |
| broker | 5800 | 6800 |
| transactions | 5900 | 6900 |

## Layer Architecture
1. **Presentation Layer**: Blazor Web App + NuxtJS Documentation
2. **Orchestration Layer**: Block Controller, Task ID Generator, Subscription Manager
3. **Business Logic Layer**: Trader, Arbitrager, DeFi, Processing Systems
4. **Data Access Layer**: EF Core, Redis clients, IPFS clients
5. **Infrastructure Layer**: Docker containers, PostgreSQL, Redis, IPFS

## Design Principles
- **Lean Methodology**: Each module does exactly one thing with minimal overhead
- **Named Constants**: All magic numbers/strings in `MLS.Core.Constants` 
- **Typed Payloads**: Every WebSocket message has a strongly-typed payload class
- **Self-Reporting**: Each module reports health, metrics, and status to BlockController
- **Container Isolation**: Each module runs in its own Docker container on the `mls-network` bridge
