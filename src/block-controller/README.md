# Block Controller — Root Orchestration Module

## Overview
The Block Controller is the central orchestration hub of the MLS platform. All modules register with it, report heartbeats to it, and route messages through it.

## Responsibilities
- Module registration and lifecycle management
- Message routing between modules (Envelope Protocol)
- Heartbeat monitoring and timeout detection
- Global state coordination
- Observatory metrics aggregation

## Ports
- HTTP API: `5100`
- WebSocket Server: `6100`

## API Endpoints
| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/modules/register` | Register a new module |
| DELETE | `/api/modules/{id}` | Deregister a module |
| GET | `/api/modules` | List all registered modules |
| GET | `/api/modules/{id}` | Get module details |
| GET | `/api/modules/{id}/health` | Get module health |
| POST | `/api/messages/route` | Route a message to a module |
| GET | `/health` | Health check |

## WebSocket Events
| Type | Direction | Description |
|------|-----------|-------------|
| `MODULE_REGISTER` | ← Client | Module registers itself |
| `MODULE_HEARTBEAT` | ← Client | Module sends heartbeat |
| `MODULE_DEREGISTER` | ← Client | Module graceful shutdown |
| `ROUTE_MESSAGE` | ← Client | Route message to another module |
| `REGISTRATION_ACK` | → Client | Confirm registration |
| `SYSTEM_EVENT` | → All | Broadcast system event |

## Namespace: `MLS.BlockController`
## Session prompt: [docs/SESSION.md](docs/SESSION.md)
