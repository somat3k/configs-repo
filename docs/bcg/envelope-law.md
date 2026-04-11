# Envelope Law
## BCG Session 04 — Mandatory Fields, Invariants, and Parse-Before-Body Rule

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, session-04-extended-document.md

---

## 1. Purpose

This document defines the envelope as the mandatory governance wrapper for all authoritative and streaming inter-module messages in the BCG ecosystem. It specifies the mandatory fields, invariants, and the foundational rule that the envelope must be parseable before the payload body is fully materialized.

---

## 2. Envelope Role

The envelope is not a payload substitute. It is the cross-cutting shell that carries the identity, routing, trace, and governance metadata required for:

- routing decisions by the Block Controller
- trace correlation across modules and sessions
- validation and admission control
- observability and audit
- causation chain reconstruction
- schema version negotiation

Every transport class (A, B, C, D) that passes through the internal fabric must carry an envelope-compatible metadata set. The physical form may differ (JSON, MessagePack, protobuf framing), but the logical fields must be resolvable.

---

## 3. Mandatory Envelope Fields

The following fields are mandatory on every authoritative or streaming message:

| Field | Type | Rule |
|-------|------|------|
| `type` | string | Must reference a `MessageTypes` constant. Never null or empty. |
| `version` | int | Must be ≥ 1. |
| `session_id` | Guid | New Guid per message. Correlation identifier. |
| `module_id` | string | Sender's registered module ID. Must match registration. |
| `timestamp` | DateTimeOffset | UTC. Must be set at creation, not at delivery. |
| `trace_id` | string | W3C `traceparent`-compatible. Stable across a request lineage. |
| `correlation_id` | Guid | Ties related messages across a logical operation. |
| `causation_id` | Guid? | The message that directly caused this message. Null for root messages. |
| `payload_schema` | string | Schema name and version. Format: `"<schema_name>:<version>"`. |
| `transport_class` | TransportClass | ClassA, ClassB, ClassC, or ClassD. |
| `routing_scope` | RoutingScope | Broadcast, Module, Topic, or Session. |
| `priority` | int | 0 (lowest) to 9 (highest). |

---

## 4. Optional Extended Fields

The following fields are optional but must be included when the scenario applies:

| Field | Condition for Inclusion |
|-------|------------------------|
| `task_id` | Present when message is part of a governed task |
| `block_id` | Present when message is scoped to a specific block |
| `target_module` | Present when routing to a specific module |
| `topic` | Present when routing to a named topic subscription |
| `tenant_id` | Present in multi-tenant deployments |
| `operator_id` | Present for operator-initiated actions |
| `artifact_ref` | Present when the message carries a Class D artifact reference |
| `tensor_ids` | Present when the message references specific tensor IDs |
| `compression` | Present when the payload body is compressed |
| `content_hash` | Present for integrity verification of the payload body |

---

## 5. Envelope Invariants

The following invariants must hold for every envelope in the BCG fabric:

1. **Parse-before-body rule**: The envelope header must be parseable and fully validated before the payload body is deserialized. Implementations must never block on payload parsing to complete envelope routing decisions.

2. **Type is never null**: An envelope without a `type` field must be rejected at ingress. The rejection must emit `TRANSPORT_ADMISSION_REJECTED`.

3. **Version is always explicit**: An envelope without a `version` field, or with `version < 1`, must be rejected.

4. **Separate versioning concerns**: The envelope version and the payload schema version are independent. The envelope version tracks the envelope contract evolution; the payload schema version tracks the business payload evolution.

5. **Correlation and causation preservation**: Correlation ID and causation ID must survive routing across modules. No routing layer may strip or reset these fields.

6. **Trace ID stability**: Within a single logical operation (request/session lineage), the trace ID must remain stable. A new root trace ID may only be created when a new independent root operation starts.

7. **Envelopes may carry multiple payload kinds**: An envelope may carry a protobuf binary payload, a JSON payload, a stream fragment, or an artifact reference. The `payload_schema` field declares which interpretation applies.

8. **No naked payloads in the fabric**: Any message traveling through the Block Controller hub or module-to-module fabric must carry a valid envelope. Messages without envelopes may not enter the governed fabric.

---

## 6. Backward Compatibility Obligation

The base `EnvelopePayload` record fields (`type`, `version`, `session_id`, `module_id`, `timestamp`, `payload`) must not be renamed, removed, or have their JSON property names changed. The `EnvelopeV2` extension adds new fields alongside the base fields.

Modules that do not yet consume `EnvelopeV2` extended fields must continue to operate correctly using the base fields alone. Extended fields are optional at the consuming side until declared mandatory by a future session.

---

## 7. Envelope Lifecycle

```
[Message Created]
       ↓
[Envelope Constructed with all mandatory fields]
       ↓
[EnvelopeValidator.Validate() — mandatory field check]
       ↓
  [Valid?]─── No ──→ [Reject: TRANSPORT_ADMISSION_REJECTED emitted]
       │
      Yes
       ↓
[Envelope Header Parsed — routing decision made]
       ↓
[Payload Body Deserialized — only after routing decision]
       ↓
[Delivered to Target / Topic / Broadcast]
       ↓
[Observability record written with envelope fields]
```

---

## 8. C# Implementation References

| Type | Location | Purpose |
|------|----------|---------|
| `EnvelopePayload` | `src/core/MLS.Core/Contracts/EnvelopePayload.cs` | Base envelope — all modules |
| `EnvelopeV2` | `src/core/MLS.Core/Contracts/EnvelopeV2.cs` | Extended envelope with Session 04 fields |
| `EnvelopeValidator` | `src/core/MLS.Core/Transport/EnvelopeValidator.cs` | Validates mandatory fields |
| `TransportClass` | `src/core/MLS.Core/Transport/TransportClass.cs` | ClassA/B/C/D enum |
| `RoutingScope` | `src/core/MLS.Core/Transport/RoutingScope.cs` | Broadcast/Module/Topic/Session enum |
| `MessageTypes.Transport` | `src/core/MLS.Core/Constants/MessageTypes.Transport.cs` | Transport event constants |
