# Live Runtime Direction Note

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## 1. Purpose

This document declares the live runtime direction for the BCG ecosystem. Live runtime continuity is a first-class product requirement. The system must evolve toward remaining alive during iterative updates. This note defines what that means, what it requires from every module, and what the implementation path looks like across sessions.

---

## 2. The Commitment

The BCG platform commits to continuous runtime presence. This means:

1. **No mandatory total restarts** for ordinary refinement cycles. A module update must not require stopping the entire platform.
2. **Rolling replacement** is the standard update pattern. New versions are introduced incrementally.
3. **Safe draining** is required before any module is stopped. In-flight requests complete before the old instance exits.
4. **Health-aware routing** during transitions. The Block Controller routes around unhealthy or draining modules.
5. **Session joinability** allows operators and observers to attach to live update sessions through dashboards and stream subscriptions.
6. **Shell governance** ensures that privileged runtime interventions are auditable and authorized.

This commitment is not aspirational. It is a product requirement that shapes implementation decisions from Session 01 onward.

---

## 3. What Live Runtime Means for Every Module

Every module must be built with runtime continuity in mind from the first line of code. This creates concrete implementation requirements:

### 3.1 Graceful Shutdown

Every module must handle `SIGTERM` and perform a graceful shutdown:

```
SIGTERM received
→ stop accepting new requests
→ drain in-flight requests (up to configured drain timeout)
→ flush pending persistence writes
→ deregister from Block Controller
→ close database and Redis connections
→ exit with code 0
```

Drain timeout must be configurable and must have a documented maximum value in the module's runtime contract.

### 3.2 Deregistration Before Exit

Every module must send a deregistration envelope to the Block Controller before exiting. The Block Controller marks the module as offline and stops routing to it. This prevents the Block Controller from routing to a dead address.

### 3.3 Block Controller Recovery

Every module must be capable of re-registering with the Block Controller without manual intervention if:
- the Block Controller restarts
- the module restarts
- the network connection is interrupted

Re-registration must happen automatically on startup and on reconnection.

### 3.4 Idempotent Operations

Operations that may be replayed during a rolling update must be idempotent. If a request is processed by both the old and new instance of a module during a handover window, the result must be the same as if it were processed once.

### 3.5 State Consistency During Replacement

When a module is being replaced:
- shared state in PostgreSQL and Redis is authoritative
- in-memory state must be considered transient and not relied upon across process boundaries
- modules must not maintain exclusive locks that prevent replacement

---

## 4. Rolling Update Pattern

A rolling update is the standard procedure for updating any BCG module.

### Step-by-Step

```
1. Prepare new image
   → build new Docker image
   → tag with new version
   → push to registry

2. Pre-update health check
   → confirm current module health is Green
   → confirm Block Controller registers the module as healthy

3. Start new instance
   → new instance starts alongside old instance
   → new instance completes startup sequence
   → new instance registers with Block Controller

4. New instance health verification
   → Block Controller monitors new instance heartbeat (3 consecutive heartbeats)
   → health probes return 200
   → Block Controller marks new instance as Healthy

5. Traffic cutover
   → Block Controller begins routing new requests to new instance
   → old instance stops receiving new requests

6. Old instance drain
   → old instance processes in-flight requests
   → drain timeout: up to configured maximum
   → old instance deregisters from Block Controller

7. Old instance termination
   → SIGTERM sent to old instance
   → old instance exits

8. Post-update verification
   → confirm only new instance is registered
   → confirm health probes green on new instance
   → confirm no error rate increase

9. Success
   → update complete, runtime continuity maintained
```

### What Must Not Happen During a Rolling Update

- The Block Controller must not go offline.
- The fabric must not lose all instances of a module simultaneously.
- In-flight requests must not be dropped without retry or client notification.
- State in PostgreSQL and Redis must not be corrupted by the transition.

---

## 5. Hot Refresh

Hot refresh is a stronger variant of rolling update for artifact changes (e.g., replacing an ONNX model file without restarting the module process).

### Hot Refresh Conditions

Hot refresh is available when:
- only the artifact (model, config, graph) has changed
- the module's process, connections, and state remain valid for the new artifact
- the new artifact passes validation before the swap

### Hot Refresh Procedure

```
1. New artifact validated
   → validation gate passes
   → new artifact registered in artifact registry

2. Swap initiated
   → new artifact loaded into memory alongside old artifact
   → new artifact bound to inference/compute path

3. Old artifact drained
   → all in-flight requests using old artifact complete (up to 500ms grace period)
   → old artifact released (GC eligible)

4. Confirmation
   → health probe confirms new artifact is active
   → Block Controller capability registry updated with new artifact version
```

The 500ms delayed dispose pattern is used to allow in-flight requests to complete before releasing the old artifact. This pattern is already implemented in `MLS.MLRuntime` (see `ModelRegistry.cs`).

---

## 6. Shell Governance

The Shell VM module provides a privileged runtime interface for controlled interventions. Shell governance rules are:

### 6.1 Ownership Semantics

Every shell command executed through the Shell VM must have:
- an assigned operator identity (who is executing the command)
- a session context (which runtime session this command belongs to)
- a declared intent (what the command is expected to do)

Commands without a declared operator identity are rejected.

### 6.2 Authorization Levels

| Level | Commands | Authorization Required |
|-------|----------|----------------------|
| L0 | Health queries, status checks | Authenticated operator |
| L1 | Service restarts, log collection | Elevated role |
| L2 | Database operations, state mutation | Explicit approval + audit |
| L3 | Network configuration, fabric topology changes | Admin role + dual approval |

### 6.3 Audit Requirements

Every command executed through the Shell VM must be logged with:
- operator identity
- timestamp (UTC)
- full command text
- exit code
- stdout/stderr summary
- session ID

Audit logs are written to PostgreSQL and are immutable.

### 6.4 Destructive Command Policy

Commands classified as destructive (deleting data, stopping services, modifying security config) require:
- explicit confirmation parameter in the command
- elevated authorization level
- audit log entry before execution begins
- notification to the Block Controller before execution

---

## 7. Session Joinability

Users and operators must be able to attach to live update sessions and observe what is happening in real time.

### 7.1 What is a Session?

A session is a live, time-bounded context in which a significant update is being applied to the BCG platform. Sessions have:
- a session ID (GUID)
- a declared scope (which modules are being updated)
- a start time
- an expected duration
- an operator identity
- a state (Preparing → Active → Draining → Complete)

### 7.2 How to Join a Session

Operators and observers join a live update session through:

- **Dashboards**: The web app observatory shows current session state, module health, and event stream.
- **Stream subscriptions**: Operators subscribe to the session's topic in the Block Controller hub. All session events are delivered in real time.
- **Status views**: The `/api/sessions/{sessionId}` HTTP endpoint returns current session state and progress.
- **Runtime-safe control actions**: Operators can send approved commands (pause, extend drain timeout, abort) through the session control API.

### 7.3 What Observers Can See

During a live session, observers see:

- current health state of all modules
- which module is being updated
- traffic cutover progress
- drain progress (percentage of in-flight requests complete)
- error rate during transition
- heartbeat status of new and old instances
- any rollback triggers that have fired

### 7.4 What Operators Can Do

During a live session, authorized operators can:

- extend the drain timeout if in-flight requests are taking longer than expected
- abort the update and trigger automatic rollback
- execute approved shell commands through the Shell VM
- manually mark a module as healthy or unhealthy in the Block Controller

Unauthorized actions are rejected by the Block Controller with an authorization error envelope.

---

## 8. Live Runtime Constraints on Implementation

These constraints must be respected by all implementation work from Session 01 onward:

| Constraint | Rule |
|-----------|------|
| No global state | Modules must not use static mutable state that prevents replacement |
| No distributed locks | Modules must not hold locks across process boundaries |
| No process-local caches as source of truth | PostgreSQL and Redis are authoritative; in-memory state is ephemeral |
| ConfigureAwait(false) in library code | All async library code must use `ConfigureAwait(false)` |
| No tight restart coupling | Module A must not require Module B to restart to recognize a change in Module A |
| Graceful degradation preferred over hard failure | If a dependency is unavailable, the module degrades gracefully rather than crashing |
| SIGTERM handled | All modules must handle SIGTERM and perform the graceful shutdown sequence |

---

## 9. Session 11 Preview

Session 11 (Live Runtime, Hot Refresh, and Session Joinability) will implement the patterns described in this note. It will produce:

- live update protocol specification
- runtime session model (C# implementation)
- safe-drain procedure (code and documentation)
- shell privilege and command authorization policy
- hot refresh QA checklist

Until Session 11, modules must implement the graceful shutdown and deregistration requirements as described in this note. Rolling updates are performed manually. The full live session tooling will be delivered in Session 11.

---

## 10. Why This Matters

Without live runtime continuity, the BCG program will depend on brittle total-restart cycles for every change. This creates:

- operator downtime during update windows
- inability to iterate quickly on production issues
- loss of accumulated in-memory state on every restart
- risk of corruption during uncoordinated restarts across multiple modules

Live runtime continuity is not a luxury. It is the operational foundation that allows the BCG ecosystem to evolve without the cost of continuous downtime.
