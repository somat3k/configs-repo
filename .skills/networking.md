---
name: networking
source: github/awesome-copilot/skills/aspire + custom
description: 'Network architecture, service discovery, WebSocket mesh, inter-module communication, port management, and distributed system networking for the MLS platform.'
---

# Networking — MLS Trading Platform

## Network Architecture
All MLS modules run on the `mls-network` Docker bridge network. Each module is a distinct network node with:
- A fixed HTTP API port (5xxx range)
- A fixed WebSocket server port (6xxx range)
- A unique `module_id` (UUID generated at startup, persisted in Redis)
- Auto-registration with Block Controller on startup

## .NET Aspire Orchestration
Use **.NET Aspire** (`Aspire.AppHost`) as the development-time orchestrator:
- AppHost project at `src/MLS.AppHost/`
- Service discovery via Aspire's environment variable injection
- Resource references: Redis, PostgreSQL wired via `.WithReference()`
- All modules added as `builder.AddProject<T>()` to AppHost

## WebSocket Server Pattern (Each Module)
```csharp
// In Program.cs of each module:
app.UseWebSockets();
app.MapWebSocketHub<ModuleHub>("/ws");
// Or using SignalR:
app.MapHub<ModuleSignalRHub>("/ws/hub");
```

## Module Registration Protocol
On startup, each module:
1. Generates or loads its `module_id` from Redis
2. POSTs registration to Block Controller: `POST http://block-controller/api/modules/register`
3. Begins heartbeat: sends `MODULE_HEARTBEAT` envelope every 5 seconds
4. Subscribes to Block Controller's command channel

## Service Discovery
- Development: .NET Aspire service discovery via env vars
- Production: DNS-based discovery via Docker network aliases
- All service URLs stored in `MLS.Core.Constants.NetworkConstants`
- Support override via environment variables: `MLS_BC_URL`, `MLS_TRADER_URL`, etc.

## Connection Resilience
- Use `Microsoft.Extensions.Http.Resilience` (Polly v8) for HTTP resilience
- Implement exponential backoff with jitter for WebSocket reconnections
- Circuit breaker pattern for all inter-module communication
- Dead letter queue in Redis for failed message delivery

## Network Mask & URL Management
- All network URLs defined in `appsettings.json` under `MLS:Network` section
- Local URLs (Docker internal): `http://{module-name}:{port}`
- Global URLs: configured per environment, stored securely in secrets vault
- `NetworkConstants` enum maps module names to their service endpoint patterns
