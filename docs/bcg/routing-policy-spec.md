# Routing Policy Specification
## BCG Session 02 — Load-Aware, Capability-Scored Routing Rules

> **Status**: ✅ Active  
> **Last Updated**: Session 02  
> **Owned by**: Block Controller (block-controller)

## 1. Purpose

This document defines the routing policy applied by the Block Controller's Route Governor when selecting a target module for any execution request. No route may be made without satisfying all gates in order.

## 2. Routing Gate Sequence

Every route decision must pass these gates in order. Failure at any gate produces a structured rejection:

| Gate | Check | Failure action |
|------|-------|---------------|
| 1. Request validity | Payload is well-formed; type is known | Reject with `ROUTE_REJECTED_INVALID_REQUEST` |
| 2. Capability match | At least one module declares the required operation | Reject with `ROUTE_REJECTED_NO_CAPABLE_MODULE` |
| 3. Health admissibility | Capable modules are in a routeable health state | Reject with `ROUTE_REJECTED_NO_HEALTHY_MODULE` |
| 4. Policy admissibility | Runtime mode permits the workload class | Reject with `ROUTE_REJECTED_POLICY_DENIED` |
| 5. Transport admissibility | A compatible transport path exists | Reject with `ROUTE_REJECTED_TRANSPORT_INCOMPATIBLE` |
| 6. Scoring and selection | Best candidate selected by score | Route to highest-scoring admissible module |

## 3. Capability Score Components

Each eligible module receives a composite score. Higher score = higher routing preference.

```
CapabilityScore = CapabilityMatchScore
               + HealthScore
               + LoadScore
               + LocalityScore
               + PriorityBonus
```

### 3.1 CapabilityMatchScore (0–100)
- **100**: module declares exact operation match
- **75**: module declares compatible superset capability
- **50**: module declares approximate match (requires transform)
- **0**: no match (not eligible)

### 3.2 HealthScore (0–50)
- **50**: Healthy
- **30**: Degraded (allowed for fallback workloads only)
- **0**: Draining / Maintenance / Quarantined (not eligible for normal routing)

### 3.3 LoadScore (0–25)
- Inversely proportional to recent in-flight request count
- **25**: module at 0% capacity utilisation
- **0**: module at or above 80% capacity utilisation

### 3.4 LocalityScore (0–15)
- **15**: module on the same Docker network segment
- **5**: module reachable but cross-segment
- **0**: module requires relay or network translation

### 3.5 PriorityBonus (0–10)
- Applied when a module has been explicitly preferred by operator policy or canary weight

## 4. Routeability States

A module may be registered but not routeable. The controller maintains routeability state separately from registration state.

| State | Receives new work | Notes |
|-------|------------------|-------|
| **Healthy** | ✅ Yes | Full routing eligibility |
| **Degraded** | ⚠️ Fallback only | Low-risk workloads only per policy |
| **Draining** | ❌ No | Finishing in-flight work; no new assignments |
| **Maintenance** | ❌ No | Operator-imposed; not for normal routing |
| **Quarantined** | ❌ No | Under investigation; no execution workloads |
| **Offline** | ❌ No | Missed heartbeat threshold; deregistered from active routing |

## 5. Execution Lane Assignment

The Route Governor assigns every request to an execution lane before routing:

| Lane | Label | Characteristics |
|------|-------|----------------|
| **A** | Immediate sync control | < 10 ms budget; no retries; control-plane only |
| **B** | Standard synchronous | < 50 ms budget; 1 retry permitted |
| **C** | Batch execution | Throughput-optimised; scheduler-driven |
| **D** | Streaming execution | Long-lived; partial result emission |
| **E** | Deferred async job | Training, large transforms; no latency SLO |

Lane assignment is based on:
- Payload type and size
- Declared module streaming support
- Current runtime mode
- Caller-declared urgency

## 6. Fallback Rules

If the primary route is rejected, the controller applies fallback in order:

1. Route to the next-highest-scoring admissible module
2. If no admissible module exists in the required lane, downgrade to Lane E (deferred)
3. If no path is safe, reject and emit `ROUTE_REJECTED_NO_SAFE_PATH`
4. Never silently drop a request in production mode

## 7. Rejection Event Structure

All rejections emit a structured `ROUTE_REJECTED` envelope payload:

```json
{
  "request_id": "<uuid>",
  "trace_id": "<w3c-traceparent>",
  "reason": "ROUTE_REJECTED_NO_HEALTHY_MODULE",
  "workload_type": "INFERENCE_REQUEST",
  "candidates_evaluated": 3,
  "candidates_admitted": 0,
  "runtime_mode": "Degraded",
  "timestamp": "<utc-iso8601>"
}
```

## 8. Policy Override Rules

Operator overrides are permitted with the following constraints:

- Every override must be audit-logged with operator identity
- Overrides must specify scope (single request, time window, or permanent policy change)
- Destructive overrides (route to quarantined module) require explicit confirmation flag
- All overrides are reversible by controller policy reset

## 9. SLO Targets

- Route decision latency p95: **< 10 ms** for Lane A and Lane B requests
- Health state convergence after heartbeat failure: **< 15 seconds** to Offline
- Rejection event delivered to operator surface: **< 5 seconds**

## 10. References

- `block-controller-authority-model.md` — authority boundaries
- `module-capability-registry-spec.md` — capability declaration format
- `health-escalation-model.md` — health state machine and escalation
- `execution-governor-sequences.md` — sequence diagrams for routing flows
