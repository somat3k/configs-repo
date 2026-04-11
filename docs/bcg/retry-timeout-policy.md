# Retry and Timeout Policy
## BCG Session 04 — Timeout Classes, Retry Rules, and Dedupe Token Semantics

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, validation-admission-rules.md

---

## 1. Purpose

This document defines the four timeout classes, the retry classification rules, the dedupe token protocol, and the delivery guarantee declarations required for every transport path in the BCG ecosystem.

---

## 2. Timeout Classes

### 2.1 Control-Plane Timeout

**Target**: p95 < 10 ms for route admission decisions inside the Block Controller.

**Applies to**:
- envelope parsing and validation
- schema version lookup
- route admission decisions
- heartbeat acknowledgement

**Behavior on timeout**:
- emit `ROUTE_REJECTED` with reason `CONTROL_PLANE_TIMEOUT`
- do not retry automatically — caller must initiate a fresh attempt
- no state change must be assumed by the caller

---

### 2.2 Execution Timeout

**Target**: varies by block type and tensor size. Must be declared per route.

**Applies to**:
- block execution requests
- tensor transformation requests
- batch execution spans
- model inference requests

**Behavior on timeout**:
- emit `EXECUTION_TIMEOUT` with block/tensor ID and elapsed time
- cancellation must propagate to any in-progress kernel or transformation
- lineage record must note the timeout as a failed execution step
- caller may retry if the operation is declared idempotent-safe

---

### 2.3 Stream Idle Timeout

**Applies to**:
- WebSocket / SignalR connections that have produced no events within the declared idle period
- gRPC server-streaming calls with no new frames

**Behavior on timeout**:
- emit `STREAM_IDLE_TIMEOUT` event before closing
- stream is closed gracefully
- subscriber may reconnect and resume (if the stream supports resumability)
- no data loss guarantee on best-effort Class B streams

---

### 2.4 Artifact Retrieval Timeout

**Applies to**:
- IPFS artifact fetch operations
- large Redis or Postgres blob retrieval

**Behavior on timeout**:
- emit `ARTIFACT_RETRIEVAL_TIMEOUT` with artifact reference and elapsed time
- do not cache a partial result as if complete
- caller may retry with the same artifact reference (dedupe token not required for reads)
- extended timeout budget allowed, but must remain observable and cancellable

---

## 3. Retry Rules

Every transport path must declare one of the following retry classifications:

### 3.1 Idempotent-Safe Retry

The operation may be retried without a dedupe token because retrying produces the same outcome regardless of execution count.

**Examples**:
- read-only queries
- capability registration (idempotent by module ID)
- heartbeat (state is always the same)
- artifact retrieval by content-addressed reference

**Behavior**: may retry automatically up to the declared retry budget with exponential backoff.

---

### 3.2 Retry with Dedupe Token

The operation may be retried, but only if a stable dedupe token is included to prevent double-execution on the receiver.

**Examples**:
- block execution commands (same block ID + task ID = dedupe key)
- tensor transformation requests
- batch execution starts
- order placement or financial state mutations

**Dedupe token protocol**:
- the dedupe token is the combination of `correlation_id` + `causation_id` + `type` + `timestamp` (truncated to second boundary)
- receivers must record seen dedupe tokens for the declared deduplication window
- if a duplicate is detected, the receiver must return the cached result (not re-execute) and emit a `DUPLICATE_REQUEST_DETECTED` event
- the deduplication window must be at least as long as the maximum retry window

---

### 3.3 Non-Retriable Mutation

The operation must not be retried by any automated mechanism. A failed execution must be surfaced to the operator.

**Examples**:
- model promotion (draft → production)
- drain commands
- destructive archive operations

**Behavior**: on failure, emit a typed error event and halt. No automatic retry. Operator must review and initiate manually.

---

### 3.4 Operator-Confirmed Retry

The operation failed and requires operator confirmation before any retry attempt is made. Used for state-changing Class A commands where silent retry would be dangerous.

**Examples**:
- any Class A command that modifies operational state and lacks a safe rollback
- execution commands during degraded module health

**Behavior**: emit `OPERATOR_ACTION_REQUIRED` event with failure context. Block further automatic attempts.

---

## 4. Dedupe Token Fields

The dedupe token is constructed by the sender from:

| Field | Source |
|-------|--------|
| `correlation_id` | `EnvelopeV2.CorrelationId` |
| `causation_id` | `EnvelopeV2.CausationId` (if present) |
| `type` | `EnvelopeV2.Type` |
| `timestamp_epoch_second` | `EnvelopeV2.Timestamp` truncated to UTC second |

The receiver must store a hash of this composite for the duration of the deduplication window.

The deduplication window must be declared per operation class in the module's capability registration.

---

## 5. Retry Budget Declaration

Every retriable path must declare:

| Field | Description |
|-------|-------------|
| `max_attempts` | Maximum number of total attempts (initial + retries) |
| `initial_delay_ms` | Delay before first retry |
| `backoff_multiplier` | Multiplier applied per subsequent retry |
| `max_delay_ms` | Maximum delay cap between retries |
| `jitter` | Whether jitter is applied (recommended for all retried paths) |

---

## 6. Delivery Guarantee Declarations

Every schema or route must declare which delivery guarantee it uses:

| Guarantee | Meaning | Typical Transport |
|-----------|---------|-------------------|
| `at-most-once` | Message may be lost; never delivered twice | Class B best-effort streams |
| `at-least-once` | Message delivered at least once; dedupe required | Class A with dedupe token |
| `ordered-per-channel` | Messages delivered in order within a topic or channel | Class B subscriptions |
| `best-effort-broadcast` | Delivered to all current subscribers; no guarantee for late joiners | Class B broadcasts |

---

## 7. Failure Class Summary

| Condition | Retry Allowed | Dedupe Required | Operator Alert |
|-----------|--------------|-----------------|----------------|
| Control-plane timeout | Yes (caller-initiated) | No | No |
| Execution timeout (idempotent) | Yes (auto) | No | No |
| Execution timeout (mutation) | No (auto) | — | Yes |
| Schema version mismatch | No | — | Yes |
| Module degraded / quarantined | Depends on policy | Depends | Yes |
| Artifact retrieval timeout | Yes (auto) | No | After N failures |
| Duplicate request detected | Not applicable | — | No (log only) |
| Non-retriable mutation failure | No | — | Yes |
