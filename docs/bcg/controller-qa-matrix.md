# Controller QA Matrix
## BCG Session 02 — QA Gates for Block Controller Authority Functions

> **Status**: ✅ Active  
> **Last Updated**: Session 02  
> **Owned by**: Block Controller (block-controller)

## 1. Purpose

This document defines the QA gate matrix for all Block Controller runtime governor functions introduced in Session 02. Every gate must be green before the controller is considered certified for that capability.

## 2. QA Gate Matrix

### 2.1 Module Registration and Capability Registry

| Gate ID | Description | Test class | Status |
|---------|-------------|-----------|--------|
| BC-REG-01 | Module registers and receives a unique moduleId | `CapabilityRegistryTests` | ⏳ |
| BC-REG-02 | Capability declaration stored and retrievable by moduleId | `CapabilityRegistryTests` | ⏳ |
| BC-REG-03 | `ResolveByOperationAsync` returns all modules matching the operation | `CapabilityRegistryTests` | ⏳ |
| BC-REG-04 | Capability update emits `MODULE_CAPABILITY_UPDATED` broadcast | `CapabilityRegistryTests` | ⏳ |
| BC-REG-05 | Eviction removes module from all operation type indexes | `CapabilityRegistryTests` | ⏳ |
| BC-REG-06 | Duplicate registration replaces previous capability record | `CapabilityRegistryTests` | ⏳ |

### 2.2 Health State Machine

| Gate ID | Description | Test class | Status |
|---------|-------------|-----------|--------|
| BC-HEALTH-01 | New module enters `Initializing` state on registration | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-02 | First successful heartbeat transitions `Initializing` → `Healthy` | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-03 | Late heartbeat transitions `Healthy` → `Degraded` | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-04 | Multiple missed heartbeats transition to `Unstable` then `Offline` | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-05 | Transition to `Degraded` emits `MODULE_DEGRADED` broadcast | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-06 | Transition to `Offline` emits `MODULE_OFFLINE` broadcast | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-07 | Operator drain command transitions module to `Draining` immediately | `HealthEscalationTests` | ⏳ |
| BC-HEALTH-08 | Module re-registration after Offline enters `Initializing` | `HealthEscalationTests` | ⏳ |

### 2.3 Route Admission

| Gate ID | Description | Test class | Status |
|---------|-------------|-----------|--------|
| BC-ROUTE-01 | Healthy module with capability match is admitted for routing | `RouteAdmissionTests` | ⏳ |
| BC-ROUTE-02 | Degraded module is rejected for standard routing (not fallback-only) | `RouteAdmissionTests` | ⏳ |
| BC-ROUTE-03 | Draining module is rejected for all new workloads | `RouteAdmissionTests` | ⏳ |
| BC-ROUTE-04 | No-capable-module produces `ROUTE_REJECTED_NO_CAPABLE_MODULE` | `RouteAdmissionTests` | ⏳ |
| BC-ROUTE-05 | No-healthy-module produces `ROUTE_REJECTED_NO_HEALTHY_MODULE` | `RouteAdmissionTests` | ⏳ |
| BC-ROUTE-06 | Highest-scoring module is selected when multiple candidates exist | `RouteAdmissionTests` | ⏳ |
| BC-ROUTE-07 | Route decision reconstructable from trace ID | `RouteAdmissionTests` | ⏳ |

### 2.4 Execution Policy

| Gate ID | Description | Test class | Status |
|---------|-------------|-----------|--------|
| BC-POLICY-01 | Lane A request assigned timeout < 10 ms | `ExecutionPolicyTests` | ⏳ |
| BC-POLICY-02 | Lane B request gets 1 retry on failure | `ExecutionPolicyTests` | ⏳ |
| BC-POLICY-03 | Lane E request has no latency SLO enforced | `ExecutionPolicyTests` | ⏳ |
| BC-POLICY-04 | Policy rejects request when runtime mode is Maintenance | `ExecutionPolicyTests` | ⏳ |
| BC-POLICY-05 | Retry budget exhaustion emits `ROUTE_REJECTED_RETRY_EXHAUSTED` | `ExecutionPolicyTests` | ⏳ |

## 3. Failure Drill Requirements

The following failure drills must be exercised before Session 02 is certified:

| Drill ID | Scenario | Expected outcome |
|----------|---------|----------------|
| FD-01 | Kill target module mid-routing | Controller detects via heartbeat timeout; emits MODULE_OFFLINE |
| FD-02 | Submit route for operation with no capable module | Structured ROUTE_REJECTED emitted within 10 ms |
| FD-03 | Submit route during Maintenance mode | Route rejected with ROUTE_REJECTED_POLICY_DENIED |
| FD-04 | Module heartbeat resumes after Degraded state | Recovery transition to Healthy; MODULE_RECOVERED emitted |
| FD-05 | Two modules compete for same operation | Highest-score module selected deterministically |

## 4. Performance Gates

| Metric | Target | Measured in |
|--------|--------|------------|
| Route decision latency p95 | < 10 ms | `RouteAdmissionTests` (timing assertions) |
| Health state convergence after missed heartbeat | < 15 s | `HealthEscalationTests` |
| Rejection event delivery | < 5 s | Integration tests (Session 17) |

## 5. Definition of Done for Session 02

Session 02 is complete when:

- [x] All 6 governance documents are committed to `docs/bcg/`
- [ ] All 22 QA gates in this matrix are marked ✅
- [ ] All 5 failure drills have been manually verified
- [ ] `dotnet test src/block-controller/MLS.BlockController.Tests/` passes with new tests
- [ ] No pre-existing tests are broken

## 6. References

- `health-escalation-model.md` — health state machine
- `routing-policy-spec.md` — route scoring and rejection
- `module-capability-registry-spec.md` — capability declaration
- `execution-governor-sequences.md` — sequence diagrams
- `src/block-controller/MLS.BlockController.Tests/` — test project
