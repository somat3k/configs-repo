---
mode: agent
description: "BCG Session 02 — Block Controller Evolution into Runtime Governor"
status: "⏳ Pending — documentation and C# implementation required"
depends-on: ["session-01"]
produces: ["docs/bcg/session-02-*.md", "src/block-controller/"]
---

# Session 02 — Block Controller Evolution into Runtime Governor

> **Status**: ⏳ Pending — governance documents and Block Controller capability upgrades not yet implemented.

## Session Goal

Upgrade the Block Controller from a passive orchestration hub into the authoritative runtime governor with execution policy, capability routing, health escalation, and live update authority.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-02-extended-document.md` — Full session narrative (source: `.prompts-update/BCG_Session_02_Extended_Document.md`)
- [ ] `block-controller-authority-model.md` — Formal authority boundary spec
- [ ] `routing-policy-spec.md` — Load-aware, capability-scored routing rules
- [ ] `module-capability-registry-spec.md` — Capability declaration and registry contract
- [ ] `health-escalation-model.md` — Heartbeat failure → degraded → drain escalation path
- [ ] `execution-governor-sequences.md` — Sequence diagrams for route, dispatch, fail, drain
- [ ] `controller-qa-matrix.md` — QA gates for controller authority functions

### C# Implementation (`src/block-controller/`)
- [ ] Add `ICapabilityRegistry` interface and `InMemoryCapabilityRegistry` implementation
- [ ] Extend `ModulesController` to accept and store capability declarations on registration
- [ ] Add routing score logic to `StrategyRouter` (capability match + health score + load factor)
- [ ] Add degradation state machine to `ModuleHealthTracker` (Healthy → Degraded → Draining → Offline)
- [ ] Add `RouteAdmission` service: rejects routes to drained/offline modules, selects best candidate
- [ ] Add `ExecutionPolicyService`: enforces timeout, retry budget, and tenant isolation rules
- [ ] Emit `MODULE_CAPABILITY_UPDATED`, `MODULE_DEGRADED`, `MODULE_DRAINED` envelope events
- [ ] Wire `OnConnectedAsync` in `BlockControllerHub` to register moduleId/clientId group routing
- [ ] Add integration tests: capability scoring, degradation escalation, route rejection

### Tests (`src/block-controller/MLS.BlockController.Tests/`)
- [ ] `CapabilityRegistryTests.cs` — register, update, score, evict
- [ ] `RouteAdmissionTests.cs` — accepts healthy, rejects degraded, selects best score
- [ ] `HealthEscalationTests.cs` — heartbeat timeout triggers state transitions
- [ ] `ExecutionPolicyTests.cs` — timeout and retry budget enforcement

## Skills to Apply

```
.skills/system-architect.md          — controller authority model, envelope discipline
.skills/websockets-inferences.md     — hub group routing, OnConnectedAsync pattern
.skills/dotnet-devs.md               — IAsyncEnumerable, Channel<T>, primary constructors
.skills/beast-development.md         — zero-alloc hot path, BoundedChannel, ServerGC
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — all events via typed EnvelopePayload
- Block Controller MUST start and accept connections with NO other modules running
- Module connections use `?moduleId=<guid>` query param on hub connect
- `IMessageRouter.BroadcastAsync` sends to `Group("broadcast")` — NOT `Clients.All`

## Acceptance Gates

- [ ] `StrategyRouter` selects modules by capability score, not round-robin
- [ ] A module whose heartbeat times out transitions to Degraded then Drained automatically
- [ ] All new controller events use typed `EnvelopePayload` with `MessageTypes.*` constants
- [ ] Integration tests pass: `dotnet test src/block-controller/MLS.BlockController.Tests/`
- [ ] 6 governance documents committed to `docs/bcg/`
- [ ] `OnConnectedAsync` assigns module connections to their moduleId group

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/block-controller/MLS.BlockController/` | Block Controller C# project |
| `src/block-controller/MLS.BlockController/Services/StrategyRouter.cs` | Routing logic to extend |
| `src/block-controller/MLS.BlockController/Services/InMemoryMessageRouter.cs` | Hub routing |
| `src/block-controller/MLS.BlockController/Controllers/ModulesController.cs` | Registration endpoint |
| `src/block-controller/MLS.BlockController/Hubs/BlockControllerHub.cs` | SignalR hub |
| `src/core/MLS.Core/Constants/MessageTypes.cs` | Add new event constants here |
| `.prompts-update/BCG_Session_02_Extended_Document.md` | Full session spec |
