# Block Controller Authority Model
## BCG Session 02 — Formal Authority Boundary Specification

> **Status**: ✅ Active  
> **Last Updated**: Session 02  
> **Owned by**: Block Controller (block-controller)

## 1. Purpose

This document defines the formal authority boundary of the Block Controller within the BCG ecosystem. It distinguishes what the controller owns, what it governs from the outside, and what it deliberately leaves to modules.

## 2. The Controller Owns

The Block Controller holds full authority over:

- **Module admission** — no module may join the fabric without passing registration and initial health checks
- **Module deregistration** — modules are removed from the registry on graceful shutdown or failure
- **Routeability state** — the controller determines whether a module may receive new work
- **Capability index** — the controller maintains the single source of truth for what each module can do
- **Health truth for routing** — health state used for routing decisions is the controller's view, not the module's self-report alone
- **Execution policy tables** — timeout classes, retry budgets, and admission rules live in the controller
- **Runtime mode state** — Normal / Degraded / Maintenance / Draining / Recovery / Incident
- **Degradation declarations** — the controller may declare a module degraded independent of the module's own signal
- **Route rejection reasons** — all rejections must be structured, logged, and attributable
- **System event broadcasts** — `MODULE_DEGRADED`, `MODULE_DRAINED`, `MODULE_CAPABILITY_UPDATED`, etc.

## 3. The Controller Does Not Own

The Block Controller must not absorb data-plane work or internal module logic:

- Per-module internal business logic (trading strategies, model weights, etc.)
- Per-module kernel implementation details
- Per-module private storage schema
- Per-module model training internals
- Per-module memory management that does not affect route safety

**Risk**: if the controller absorbs data-plane processing it becomes a bottleneck. All heavy execution must remain in module species.

## 4. Shared Ownership Zones

Some domains require coordination between the controller and modules:

| Domain | Controller responsibility | Module responsibility |
|--------|--------------------------|----------------------|
| Tensor contracts | Validate transport admissibility | Declare input/output tensor classes |
| Transport compatibility | Enforce class rules | Declare supported transport interfaces |
| Observability metadata | Aggregate and broadcast | Emit per-operation telemetry |
| Execution status | Track and expose | Report completion and failure |
| Rollback events | Broadcast and coordinate | Acknowledge and respond |
| Admission certification | Apply certification gate | Submit and maintain capability declaration |

## 5. Authority Hierarchy

```
Block Controller (Runtime Governor)
├── Module Registry           — admission, lifecycle, identity
├── Capability Resolver       — eligible module discovery per workload
├── Health Governor           — health state tracking and escalation
├── Route Governor            — scoring, selection, and rejection
├── Execution Policy Engine   — timeout, retry, lane assignment
└── Runtime Mode Manager      — Normal / Degraded / Maintenance / Draining
    └── Module Species (independent)
        ├── Internal business logic (not controller-owned)
        ├── Internal kernel implementation (not controller-owned)
        └── Declared capabilities (shared with controller)
```

## 6. Authority Enforcement Rules

1. The controller MUST reject registration requests with incomplete or ambiguous capability declarations
2. The controller MUST NOT route to modules in Draining or Quarantined states for new workloads
3. The controller MUST emit a structured rejection event when a route is denied
4. Every route decision MUST be reconstructable by trace ID
5. Operator shell actions that change routeability MUST be audit-logged
6. The controller MUST prefer runtime continuity over speed when the two conflict

## 7. Governance Drift Prevention

Signs of authority drift that must be corrected:

- A module routes to another module directly without controller knowledge
- A capability is assumed rather than declared
- A module can change its own health state in the controller's registry without validation
- Route decisions are made inside a module rather than by the controller
- The controller holds payload data that belongs to a module species

## 8. References

- `session-02-extended-document.md` — full session narrative
- `routing-policy-spec.md` — route scoring and selection rules
- `module-capability-registry-spec.md` — capability declaration format
- `health-escalation-model.md` — health state machine
- `execution-governor-sequences.md` — sequence diagrams
