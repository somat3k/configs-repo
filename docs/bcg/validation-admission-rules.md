# Validation and Admission Rules
## BCG Session 04 — Intake Validation Checklist Per Transport Class

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, envelope-law.md, compatibility-versioning-policy.md

---

## 1. Purpose

This document defines the validation and admission rules that every ingress path must satisfy before a message enters the BCG internal fabric. It specifies the checks required per transport class and the obligations of the Block Controller as the governing admission authority.

---

## 2. Universal Intake Validation (All Classes)

Every message entering the BCG fabric, regardless of transport class, must pass all of the following checks:

| Check | Requirement | Failure Action |
|-------|-------------|---------------|
| Envelope structure | Envelope must parse without error | Reject — emit `TRANSPORT_ADMISSION_REJECTED` |
| `type` field | Must be non-null, non-empty, reference a known `MessageTypes` constant | Reject |
| `version` field | Must be ≥ 1 | Reject |
| `module_id` field | Must be non-null, non-empty | Reject |
| `timestamp` field | Must be present, must not be in the future beyond clock-skew tolerance (5 s) | Reject |
| `transport_class` field | Must be one of ClassA, ClassB, ClassC, ClassD | Reject |
| `payload_schema` field | Must be non-null, follow `"<schema>:<version>"` format | Reject |
| Payload size | Must not exceed the declared maximum for the transport class | Reject with `PAYLOAD_SIZE_EXCEEDED` |

---

## 3. Class A — Authoritative Machine Contract Admission

In addition to the universal checks:

| Check | Requirement | Failure Action |
|-------|-------------|---------------|
| Schema version support | Receiving module must declare support for the `payload_schema` version | Reject — `SCHEMA_VERSION_MISMATCH` |
| Proto lint (pre-deployment) | Schema must have passed `buf lint` before deployment | Reject promotion to production |
| Correlation ID | Must be present and non-empty Guid | Reject |
| Trace ID | Must be present, W3C traceparent-compatible format | Reject |
| Module authorization | `module_id` must match an active registered module | Reject |
| Routing scope | Must be `Module` or `Topic` for Class A (not Broadcast for state commands) | Reject |
| State-changing commands | Must include dedupe token or idempotency key | Reject without dedupe token |
| Integrity hash | Must be present and valid where declared in the schema | Reject — `INTEGRITY_CHECK_FAILED` |

---

## 4. Class B — Live Event and Stream Fabric Admission

In addition to the universal checks:

| Check | Requirement | Failure Action |
|-------|-------------|---------------|
| Event type declared | `type` must reference a valid `MessageTypes` constant | Reject |
| Rate limit | Event rate must not exceed declared stream budget for the emitting module | Drop + `STREAM_BACKPRESSURE_EVENT` |
| Connection authorization | Emitting module must hold an active SignalR/WebSocket connection | Drop silently |
| Envelope present | Envelope must be attached even for streaming frames | Drop frame |

Note: Class B failures are softer than Class A. A malformed frame must be discarded and logged, but must not terminate the stream for the consumer.

---

## 5. Class C — External Trigger and Compatibility Ingress Admission

In addition to the universal checks:

| Check | Requirement | Failure Action |
|-------|-------------|---------------|
| Payload schema validation | Payload must validate against declared intake schema | 422 Unprocessable Entity |
| Caller authentication | Caller must present valid credentials | 401 Unauthorized |
| Caller authorization | Caller must be permitted for the declared operation | 403 Forbidden |
| DataEvolution gate | If payload is not BCG-native, it must route to DataEvolution before internal admission | Route to DataEvolution |
| No naked external payloads | Raw external payload may not bypass intake | Reject without DataEvolution normalization |
| Envelope enrichment | Envelope fields (`trace_id`, `correlation_id`, `module_id`) must be assigned at intake | Enrichment performed by intake handler |

---

## 6. Class D — Artifact Reference Admission

In addition to the universal checks:

| Check | Requirement | Failure Action |
|-------|-------------|---------------|
| Artifact reference present | `artifact_ref` field must be populated | Reject |
| Integrity hash | `content_hash` must be present | Reject |
| Storage location declared | Must reference one of: Redis key, Postgres record ID, IPFS CID | Reject |
| Retrieval authorization | Consuming module must be authorized to retrieve the referenced artifact | Reject — `ARTIFACT_ACCESS_DENIED` |
| Lineage linkage | Artifact reference must link back to a tensor ID or operation ID | Reject |

---

## 7. Block Controller Admission Authority

The Block Controller is the governing admission authority for all messages passing through the controller hub. Its responsibilities are:

1. **Registration compliance**: Verify that modules have completed registration before accepting their messages.
2. **Schema version support**: Cross-check declared `payload_schema` against registered module capability versions.
3. **Route legitimacy**: Verify that the declared `target_module` or `topic` exists and is reachable.
4. **Transport class enforcement**: Verify that the transport class matches the declared route policy.
5. **Compatibility mismatch detection**: If `payload_schema` version is not supported by the target, emit `TRANSPORT_COMPATIBILITY_ERROR` rather than routing.
6. **Degradation handling**: If a target module is in a degraded or quarantined state, apply the execution policy (reject or queue) and emit the appropriate governance event.

---

## 8. Internal Admission Requirements

A message admitted into the internal fabric must have:

- normalized identifiers (no null, no whitespace-only IDs)
- trace metadata (`trace_id`, `correlation_id`, `causation_id` where applicable)
- validated payload shape or schema fingerprint
- explicit route class
- observability tags set (at minimum: `type`, `module_id`, `transport_class`, `routing_scope`)

Data that does not conform must be:
- rejected (with typed error event), or
- quarantined (operator alert), or
- routed to DataEvolution for normalization, or
- down-converted through a declared compatibility adapter

No silent reinterpretation is allowed.

---

## 9. EnvelopeValidator Contract

The `EnvelopeValidator` type in `src/core/MLS.Core/Transport/EnvelopeValidator.cs` implements the universal intake validation checks. It must:

- return a `ValidationResult` that is either `Valid` or carries a list of `ValidationError` entries
- never throw on malformed input — all errors must be encoded in the result
- be callable synchronously with zero allocation on the happy path
- be covered by tests in `src/core/MLS.Core.Tests/Transport/EnvelopeValidatorTests.cs`
