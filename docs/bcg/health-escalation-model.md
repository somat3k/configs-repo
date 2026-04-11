# Health Escalation Model
## BCG Session 02 — Heartbeat Failure and Degradation Escalation Path

> **Status**: ✅ Active  
> **Last Updated**: Session 02  
> **Owned by**: Block Controller (block-controller)

## 1. Purpose

This document defines the health state machine for registered modules, including the escalation path from healthy through degraded, draining, and offline states, and the rules that govern each transition.

## 2. Health States

| State | Code | Routing eligible | Description |
|-------|------|-----------------|-------------|
| **Healthy** | `Healthy` | ✅ Full | Heartbeats on time; no failures observed |
| **Initializing** | `Initializing` | ⚠️ Control only | Module registered but not yet confirmed ready |
| **Degraded** | `Degraded` | ⚠️ Fallback only | Heartbeat jitter or soft failures detected |
| **Unstable** | `Unstable` | ❌ No | Repeated hard failures or missed heartbeats |
| **Maintenance** | `Maintenance` | ❌ No | Operator-imposed; no routing |
| **Draining** | `Draining` | ❌ New work | Finishing in-flight work; no new assignments |
| **Quarantined** | `Quarantined` | ❌ No | Under investigation; emergency stop |
| **Offline** | `Offline` | ❌ No | Deregistered or declared failed by controller |

## 3. State Transition Rules

### 3.1 Healthy → Degraded
Triggered when ANY of the following occur:
- Heartbeat arrives > 2× the expected interval (default: > 10 s for 5 s interval)
- One heartbeat is missed within a 30-second window
- Module self-reports a soft failure in the heartbeat payload

### 3.2 Degraded → Unstable
Triggered when:
- Two or more heartbeats missed consecutively within 30 s
- Module self-reports a hard failure

### 3.3 Unstable → Offline
Triggered when:
- Three consecutive heartbeats missed (default: > 15 s for 5 s interval)

### 3.4 Any State → Maintenance
Triggered by:
- Explicit operator command via `POST /api/modules/{moduleId}/maintenance`
- Shell command with `MAINTENANCE_DECLARE` envelope type

Transition is immediate. Exiting maintenance requires explicit operator `MAINTENANCE_CLEAR`.

### 3.5 Any State → Draining
Triggered by:
- `POST /api/modules/{moduleId}/drain` (Session 11 operator command)
- Module sends graceful shutdown signal

On drain: in-flight work completes within policy timeout; then state transitions to Offline.

### 3.6 Any State → Quarantined
Triggered by:
- Operator command with confirmation flag required
- Repeated routing failures above threshold within a 5-minute window

Quarantine can only be cleared by an Admin-role operator action.

### 3.7 Offline / Any → Healthy
A module that re-registers after being offline re-enters the `Initializing` state and must pass one successful heartbeat to reach `Healthy`.

## 4. State Machine Diagram

```
                ┌─────────────┐
                │ Initializing│◄─── registration
                └──────┬──────┘
                       │ first heartbeat OK
                       ▼
                ┌─────────────┐
          ┌────►│   Healthy   │◄────────────────────────────────────┐
          │     └──────┬──────┘                                     │
          │            │ heartbeat late / soft failure               │
          │            ▼                                             │
          │     ┌─────────────┐                                      │
          │     │  Degraded   │──── heartbeats recover ─────────────┘
          │     └──────┬──────┘
          │            │ 2+ missed heartbeats
          │            ▼
          │     ┌─────────────┐
          │     │  Unstable   │
          │     └──────┬──────┘
          │            │ 3 consecutive missed
          │            ▼
          │     ┌─────────────┐
          │     │   Offline   │◄─── explicit deregister
          │     └──────┬──────┘
          │            │ re-registration
          └────────────┘
  
  At any point (operator):
  Any ──► Maintenance (explicit command)
  Any ──► Draining (graceful shutdown or operator drain)
  Any ──► Quarantined (emergency operator command)
```

## 5. Escalation Timings (Defaults)

| Event | Default threshold | Configurable |
|-------|-----------------|-------------|
| Heartbeat interval (module side) | 5 seconds | Yes (per module) |
| Healthy → Degraded (late heartbeat) | 10 seconds since last | Yes |
| Degraded → Unstable (consecutive misses) | 2 misses in 30 s | Yes |
| Unstable → Offline (consecutive misses) | 3 misses (≈ 15 s) | Yes |

## 6. Broadcast Events

All health transitions emit a broadcast envelope:

| Transition | Event type | Broadcast group |
|------------|-----------|----------------|
| → Degraded | `MODULE_DEGRADED` | `broadcast` |
| → Draining | `MODULE_DRAINED` | `broadcast` |
| → Offline | `MODULE_OFFLINE` | `broadcast` |
| → Healthy (recovery) | `MODULE_RECOVERED` | `broadcast` |
| → Maintenance | `MODULE_MAINTENANCE` | `broadcast` |
| → Quarantined | `MODULE_QUARANTINED` | `broadcast` |

## 7. Recovery Rules

A module returning to Healthy must:
1. Successfully re-register (if offline)
2. Pass at least one heartbeat without errors
3. Receive `Healthy` state assignment from the controller (not self-assigned)

## 8. Observability Requirements

- Every state transition must be logged with: moduleId, previousState, newState, reason, timestamp, traceId
- Transition events emitted as typed `EnvelopePayload` to the `broadcast` group
- State history queryable for last 100 transitions per module (Session 15 full forensics)

## 9. References

- `routing-policy-spec.md` — HealthScore usage in route scoring
- `block-controller-authority-model.md` — authority over health state
- `src/block-controller/MLS.BlockController/Services/ModuleHealthTracker.cs` — implementation
- `src/core/MLS.Core/Constants/MessageTypes.cs` — `MODULE_DEGRADED`, `MODULE_DRAINED` constants
