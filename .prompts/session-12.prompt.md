---
mode: agent
description: "BCG Session 12 — Dynamic Ports, Network Masking, and Discovery"
status: "⏳ Pending — dynamic endpoint registration and discovery not implemented"
depends-on: ["session-02", "session-11"]
produces: ["docs/bcg/session-12-*.md", "src/network-modules/", "src/block-controller/"]
---

# Session 12 — Dynamic Ports, Network Masking, and Discovery

> **Status**: ⏳ Pending — ports are currently static; dynamic endpoint registration and environment routing need to be governed.

## Session Goal

Strengthen the network layer for future-proof module attachment under dynamic ports, environment-aware routing, and secure endpoint discovery without hardcoded addresses.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-12-extended-document.md` (source: `.prompts-update/BCG_Session_12_Extended_Document.md`)
- [ ] `discovery-protocol.md` — module advertises endpoint on register; BC stores and serves discovery
- [ ] `endpoint-resolution-spec.md` — resolution priority: env override → registry → default port table
- [ ] `dynamic-port-governance.md` — port assignment rules, conflict detection, static fallback table
- [ ] `environment-routing-matrix.md` — dev / stage / prod routing differences and isolation rules

### Network Modules (`src/network-modules/`)
- [ ] Extend `network-mask` module — add `IEndpointRegistry` interface: `RegisterEndpoint`, `ResolveEndpoint`, `InvalidateEndpoint`
- [ ] Add `EndpointRecord.cs` — record: moduleId, httpUrl, wsUrl, environment, lastSeen, ttlSeconds
- [ ] Add Redis-backed `RedisEndpointRegistry.cs` — stores `EndpointRecord` with TTL
- [ ] Add `GET /api/endpoints` to network-mask — returns all active module endpoints
- [ ] Add `GET /api/endpoints/{moduleId}` — returns single module endpoint

### Block Controller: Discovery Integration (`src/block-controller/`)
- [ ] Extend registration payload to include `httpUrl` and `wsUrl` from registering module
- [ ] Store endpoint in `IEndpointRegistry` on registration
- [ ] Invalidate endpoint on heartbeat timeout or drain completion
- [ ] Route resolution in `StrategyRouter` uses `IEndpointRegistry` — not hardcoded port table
- [ ] Emit `ENDPOINT_REGISTERED`, `ENDPOINT_INVALIDATED`, `ENDPOINT_RESOLVED` events

### Module Alignment
- [ ] All modules: read HTTP/WS URLs from environment variables (`MLS_HTTP_URL`, `MLS_WS_URL`) rather than config literals
- [ ] Update `BlockControllerClient` in all modules to include `httpUrl` and `wsUrl` in registration body
- [ ] Add `MLS_HTTP_URL` and `MLS_WS_URL` to `docker-compose.yml` environment sections

### Tests (`src/network-modules/`)
- [ ] `EndpointRegistryTests.cs` — register, resolve, TTL expiry, conflict detection
- [ ] `DynamicRoutingTests.cs` — route resolves through registry, falls back to static table when registry empty

## Skills to Apply

```
.skills/networking.md                — .NET Aspire service discovery, dynamic endpoint registration
.skills/dotnet-devs.md               — IOptions<T>, IHostedService, environment variables
.skills/storage-data-management.md   — Redis TTL for endpoint records
.skills/system-architect.md          — environment isolation, discovery governance
.skills/websockets-inferences.md     — hub reconnect after endpoint change
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — discovery events via typed EnvelopePayload
- ALL blockchain/module addresses stored in PostgreSQL or Redis registry — NEVER hardcoded
- Module ports from environment variables — static port table is ONLY a fallback, not primary
- No untyped cross-module payload in discovery paths

## Acceptance Gates

- [ ] Module registers with dynamic URL; Block Controller stores it in `IEndpointRegistry`
- [ ] `StrategyRouter` resolves route URL from registry, not from hardcoded constant
- [ ] Endpoint TTL expires after heartbeat timeout and module is removed from registry
- [ ] All new tests pass: `dotnet test src/network-modules/`
- [ ] 4 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/network-modules/` | network-mask and related modules |
| `src/block-controller/MLS.BlockController/Controllers/ModulesController.cs` | Registration endpoint to extend |
| `src/block-controller/MLS.BlockController/Services/StrategyRouter.cs` | Route resolution to extend |
| `src/modules/trader/MLS.Trader/Services/BlockControllerClient.cs` | Reference registration client |
| `docker-compose.yml` | Add MLS_HTTP_URL, MLS_WS_URL env vars |
| `.prompts-update/BCG_Session_12_Extended_Document.md` | Full session spec |
