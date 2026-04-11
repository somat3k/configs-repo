# Execution Governor Sequence Diagrams
## BCG Session 02 — Route, Dispatch, Fail, and Drain Sequences

> **Status**: ✅ Active  
> **Last Updated**: Session 02  
> **Owned by**: Block Controller (block-controller)

## 1. Purpose

This document provides Mermaid sequence diagrams for the four primary execution governance flows: standard route, route rejection, module degradation, and graceful drain.

## 2. Standard Route Flow

```mermaid
sequenceDiagram
    participant Caller as Module / Client
    participant Hub as BlockControllerHub
    participant Admission as RouteAdmissionService
    participant Caps as ICapabilityRegistry
    participant Health as ModuleHealthTracker
    participant Router as IMessageRouter
    participant Target as Target Module

    Caller->>Hub: SendEnvelope(INFERENCE_REQUEST)
    Hub->>Admission: AdmitRouteAsync(envelope)
    Admission->>Caps: ResolveByOperationAsync("INFERENCE_REQUEST")
    Caps-->>Admission: [ml-runtime-id]
    Admission->>Health: GetHealthStateAsync(ml-runtime-id)
    Health-->>Admission: Healthy
    Admission->>Admission: ComputeScore(capability=100, health=50, load=20)
    Admission-->>Hub: RouteResult(targetId=ml-runtime-id, lane=B, score=170)
    Hub->>Router: RouteAsync(envelope → ml-runtime-id)
    Router->>Target: ReceiveEnvelope(INFERENCE_REQUEST)
    Target-->>Router: (processes request)
    Target->>Hub: SendEnvelope(INFERENCE_RESULT)
    Hub->>Router: RouteAsync(envelope → caller-id)
    Router->>Caller: ReceiveEnvelope(INFERENCE_RESULT)
```

## 3. Route Rejection Flow

```mermaid
sequenceDiagram
    participant Caller as Module / Client
    participant Hub as BlockControllerHub
    participant Admission as RouteAdmissionService
    participant Caps as ICapabilityRegistry
    participant Health as ModuleHealthTracker
    participant Router as IMessageRouter

    Caller->>Hub: SendEnvelope(INFERENCE_REQUEST)
    Hub->>Admission: AdmitRouteAsync(envelope)
    Admission->>Caps: ResolveByOperationAsync("INFERENCE_REQUEST")
    Caps-->>Admission: [ml-runtime-id]
    Admission->>Health: GetHealthStateAsync(ml-runtime-id)
    Health-->>Admission: Draining
    Admission->>Admission: ScoreCheck — HealthScore=0, not admissible
    Admission-->>Hub: RouteResult(rejected=true, reason=ROUTE_REJECTED_NO_HEALTHY_MODULE)
    Hub->>Router: BroadcastAsync(ROUTE_REJECTED envelope)
    Router->>Caller: ReceiveEnvelope(ROUTE_REJECTED)
```

## 4. Heartbeat Failure and Degradation Flow

```mermaid
sequenceDiagram
    participant Module as Module (ml-runtime)
    participant Controller as Block Controller
    participant Health as ModuleHealthTracker
    participant Router as IMessageRouter

    Module-->>Controller: (heartbeat silent — missed interval 1)
    Health->>Health: RecordMissedHeartbeat(ml-runtime-id)
    Note over Health: 1 miss — state remains Healthy

    Module-->>Controller: (heartbeat silent — missed interval 2)
    Health->>Health: RecordMissedHeartbeat(ml-runtime-id)
    Health->>Health: TransitionState(Healthy → Degraded)
    Health->>Router: BroadcastAsync(MODULE_DEGRADED)

    Module-->>Controller: (heartbeat silent — missed interval 3)
    Health->>Health: RecordMissedHeartbeat(ml-runtime-id)
    Health->>Health: TransitionState(Degraded → Unstable)

    Module-->>Controller: (heartbeat silent — missed interval 4+)
    Health->>Health: TransitionState(Unstable → Offline)
    Health->>Router: BroadcastAsync(MODULE_OFFLINE)
    Note over Health: Module removed from routing candidates
```

## 5. Graceful Drain Flow

```mermaid
sequenceDiagram
    participant Operator as Operator / Shell
    participant Controller as Block Controller HTTP API
    participant Health as ModuleHealthTracker
    participant Module as Target Module
    participant Router as IMessageRouter

    Operator->>Controller: POST /api/modules/{moduleId}/drain
    Controller->>Health: TransitionStateAsync(moduleId, Draining)
    Health->>Router: BroadcastAsync(MODULE_DRAINED)
    Note over Health: No new workloads routed to module

    Module->>Module: (finishes in-flight work)
    Module->>Controller: DELETE /api/modules/{moduleId} (deregister)
    Controller->>Health: TransitionStateAsync(moduleId, Offline)
    Health->>Router: BroadcastAsync(MODULE_OFFLINE)
    Note over Controller: Module slot available for replacement
```

## 6. Module Recovery Flow

```mermaid
sequenceDiagram
    participant Module as Module (replacement instance)
    participant Controller as Block Controller HTTP API
    participant Registry as IModuleRegistry
    participant Caps as ICapabilityRegistry
    participant Health as ModuleHealthTracker
    participant Router as IMessageRouter

    Module->>Controller: POST /api/modules/register (with capabilities)
    Controller->>Registry: RegisterAsync(request)
    Registry-->>Controller: ModuleRegistration(moduleId)
    Controller->>Caps: RegisterAsync(moduleId, capabilityRecord)
    Controller->>Health: SetStateAsync(moduleId, Initializing)

    Module->>Controller: PATCH /api/modules/{moduleId}/heartbeat
    Controller->>Health: RecordHeartbeatAsync(moduleId)
    Health->>Health: TransitionState(Initializing → Healthy)
    Health->>Router: BroadcastAsync(MODULE_RECOVERED)
    Note over Health: Module now eligible for routing
```

## 7. References

- `routing-policy-spec.md` — route scoring and admission rules
- `health-escalation-model.md` — state machine details
- `module-capability-registry-spec.md` — capability declaration
- `src/block-controller/MLS.BlockController/Services/RouteAdmissionService.cs` — implementation
