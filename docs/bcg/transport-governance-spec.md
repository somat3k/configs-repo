# Transport Governance Specification
## BCG Session 04 — Class Rules, Ownership, and Failure Semantics

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: session-04-extended-document.md, envelope-law.md

---

## 1. Purpose

This document specifies the four transport class rules, declares ownership obligations, and defines failure semantics for all inter-module communication in the BCG ecosystem. Every message path must be classified under one of the four classes before it may enter production.

---

## 2. Transport Class Definitions

### Class A — Authoritative Machine Contracts

**Used for**:
- module registration and acknowledgement
- capability reporting and updates
- route execution commands
- tensor transformation requests and responses
- batch execution orchestration
- health state streaming
- model activation and promotion
- stateful runtime commands

**Transport**: protobuf + gRPC

**Ownership**: the module that owns the operation owns the proto schema. The Block Controller owns all orchestration and admission schemas.

**Requirements**:
- explicit proto schema with package and version
- forward-compatible field additions only (no silent field removal)
- strict server-side validation of all required semantic fields
- deterministic binary serialization via protobuf
- contract tests required before promotion to production

**Failure semantics**:
- malformed schema → reject immediately, emit `TRANSPORT_ADMISSION_REJECTED`
- version mismatch → emit `SCHEMA_VERSION_MISMATCH`, halt route
- timeout → deterministic error response, no silent retry
- incompatible consumer → emit `TRANSPORT_COMPATIBILITY_ERROR`, do not deliver

---

### Class B — Live Event and Stream Fabric

**Used for**:
- operator session updates and UI feeds
- runtime observability streams
- partial outputs and incremental results
- topic subscription updates
- live notifications and telemetry
- human-visible orchestration events

**Transport**: WebSocket / SignalR (binary MessagePack framing preferred on hot event lanes)

**Ownership**: the emitting module owns the event type; the Block Controller owns broadcast routing.

**Requirements**:
- envelope metadata always attached (`EnvelopeV2` or compatible)
- bounded rate — consumers must declare backpressure tolerance
- event type must be declared in `MessageTypes` constants
- resumability or replay policy must be declared per event type

**Failure semantics**:
- disconnected consumer → drop or buffer per declared delivery guarantee
- backpressure exceeded → drop and emit `STREAM_BACKPRESSURE_EVENT`
- malformed frame → discard frame, log, continue stream
- unrecognized event type → consumer ignores and continues (forward-compatible)

---

### Class C — External Trigger and Compatibility Ingress

**Used for**:
- webhook intake from external systems
- external partner integrations
- controlled REST ingress
- backward-compatible management endpoints

**Transport**: HTTP/JSON or typed HTTP with schema validation

**Ownership**: DataEvolution for normalization; the intake API owner for validation.

**Requirements**:
- strict intake validation against declared schema
- payload must be normalized through DataEvolution or a declared compatibility adapter before internal routing
- envelope enrichment required before payload enters the internal fabric
- no raw external payload may bypass intake validation

**Failure semantics**:
- malformed payload → 400 Bad Request, log intake error
- schema version not supported → 422 Unprocessable Entity, emit `SCHEMA_VERSION_MISMATCH`
- unauthorized caller → 401/403, no internal routing attempted
- DataEvolution normalization failure → 422, lineage record written for audit

---

### Class D — Artifact and Large Object References

**Used for**:
- large tensor payload references beyond inline threshold
- model artifacts
- large dataset and snapshot references
- replay packages

**Transport**: metadata envelope travels inline; artifact body travels via Redis/Postgres/IPFS reference.

**Ownership**: producing module declares the artifact reference; storage routing policy (session-03 tensor storage threshold) governs location.

**Requirements**:
- integrity fields mandatory (`content_hash`, `artifact_ref`)
- retrieval authorization declared per artifact type
- lineage linkage back to the originating tensor or operation
- no naked blob URLs — all references must use governed location descriptors

**Failure semantics**:
- reference not found → typed `ARTIFACT_NOT_FOUND` error, not a generic 404
- integrity mismatch on fetch → reject and escalate, do not deliver corrupt artifact
- retrieval timeout → emit timeout event, allow caller-side retry with dedupe token

---

## 3. Path Ownership Rules

Every production message path must declare:

| Field | Description |
|-------|-------------|
| `owner` | Module or team responsible for schema evolution |
| `transport_class` | One of ClassA, ClassB, ClassC, ClassD |
| `schema_version` | Current schema version (semver or integer) |
| `compatibility_rule` | backward / forward / breaking (requires migration note) |
| `timeout_class` | control-plane / execution / stream-idle / artifact |
| `retry_rule` | idempotent-safe / dedupe-token / non-retriable / operator-confirmed |
| `observability_contract` | Minimum fields that must appear in telemetry for this path |
| `failure_behavior` | Explicit: reject / quarantine / DataEvolution / compatibility adapter |

Paths without all fields declared may not enter production.

---

## 4. Failure Behavior Taxonomy

| Class | Behavior | Action |
|-------|----------|--------|
| `reject` | Message does not enter fabric | Emit typed error envelope, log |
| `quarantine` | Message held for manual review | Hold in quarantine topic, alert operator |
| `DataEvolution` | Route through normalization bridge | DataEvolution emits normalized tensor with lineage |
| `compatibility-adapter` | Convert through declared shim | Adapter version-stamps converted payload |
| `drop` | Best-effort, no error required | Streaming Class B paths only |
| `operator-confirmed-retry` | Block until operator confirms | State-changing Class A paths only |

---

## 5. Cross-Class Routing Prohibition

- Class C payloads must not be routed directly into Class A lanes without normalization.
- Class D references must not substitute for Class A structural messages.
- Class B events must not be used to command state changes that require Class A semantics.
- No class may be silently downgraded by the routing layer without a governance event emitted.
