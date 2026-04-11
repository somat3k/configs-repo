---
mode: agent
description: "BCG Session 04 — Protobuf, Envelope, and Transport Unification"
status: "🔧 Partial — constitution document exists; C# envelope extensions and proto files pending"
depends-on: ["session-01", "session-02", "session-03"]
produces: ["docs/bcg/session-04-*.md", "src/core/MLS.Core/Transport/", "src/core/MLS.Core/Contracts/Transport/"]
---

# Session 04 — Protobuf, Envelope, and Transport Unification

> **Status**: 🔧 Partial — `docs/bcg/session-04-extended-document.md` exists.  
> C# transport extensions, envelope v2 fields, and protobuf package map are pending.

## Session Goal

Govern all inter-module communication under one transport constitution: protobuf/gRPC for authoritative lanes, SignalR/WebSocket for live events, HTTP for ingress, and a typed envelope carrying cross-cutting metadata on every message.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [x] `session-04-extended-document.md` — transport constitution
- [ ] `transport-governance-spec.md` — Class A/B/C/D rules, ownership, failure semantics
- [ ] `protobuf-package-map.md` — `bcg.*` package ownership table with owning module per package
- [ ] `envelope-law.md` — mandatory fields, invariants, parseable-before-body rule
- [ ] `compatibility-versioning-policy.md` — backward/forward/breaking change definitions
- [ ] `transport-selection-matrix.md` — decision table: when to use gRPC vs WS vs HTTP vs artifact ref
- [ ] `validation-admission-rules.md` — intake validation checklist per transport class
- [ ] `retry-timeout-policy.md` — timeout classes, retry rules, dedupe token semantics
- [ ] `transport-observability-checklist.md` — required metrics/traces per transport class
- [ ] `migration-note-template.md` — template for future schema breaking-change documentation

### C# Envelope Extensions (`src/core/MLS.Core/Contracts/`)
- [ ] Extend `EnvelopePayload` (or create `EnvelopeV2`) to include:
  - `TraceId` (string, W3C traceparent compatible)
  - `CorrelationId` (Guid)
  - `CausationId` (Guid?)
  - `PayloadSchema` (string — schema name + version)
  - `TransportClass` (enum: ClassA, ClassB, ClassC, ClassD)
  - `RoutingScope` (enum: Broadcast, Module, Topic, Session)
  - `Priority` (int, 0–9)
- [ ] Create `TransportClass.cs` enum in `src/core/MLS.Core/Transport/`
- [ ] Create `RoutingScope.cs` enum in `src/core/MLS.Core/Transport/`
- [ ] Create `EnvelopeValidator.cs` — validates mandatory fields, version ≥ 1, no null type
- [ ] Add `MessageTypes.Transport.cs` — `TRANSPORT_ADMISSION_REJECTED`, `TRANSPORT_COMPATIBILITY_ERROR`, `SCHEMA_VERSION_MISMATCH`

### Protobuf Files (`src/proto/`)
- [ ] Create `src/proto/bcg/module/module.proto` — RegisterModule, RegisterAck, HealthEvent, CapabilityUpdate
- [ ] Create `src/proto/bcg/block/block.proto` — BlockRequest, BlockResponse, StreamExecuteFragment
- [ ] Create `src/proto/bcg/tensor/tensor.proto` — BcgTensorProto, TensorShape, TensorReference
- [ ] Create `src/proto/bcg/session/session.proto` — SessionJoinRequest, SessionStateEvent, OperatorControlAck
- [ ] Create `src/proto/bcg/observability/observability.proto` — RouteEvent, TransportMetric
- [ ] Add `Directory.Build.props` protobuf tooling config if not present

### Tests (`src/core/MLS.Core.Tests/Transport/`)
- [ ] `EnvelopeValidatorTests.cs` — rejects missing type, missing version, null payload
- [ ] `EnvelopeV2SerializationTests.cs` — round-trip JSON + MessagePack, correlation preserved
- [ ] `TransportClassEnumTests.cs` — all enum values serializable

## Skills to Apply

```
.skills/websockets-inferences.md     — SignalR hub routing, envelope schema
.skills/system-architect.md          — transport class model, route governance
.skills/dotnet-devs.md               — records, enums, C# 13, ConfigureAwait
.skills/beast-development.md         — MessagePack serialization, Span<byte>, ArrayPool
.skills/networking.md                — gRPC, protobuf, HTTP/2 transport
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — extended envelope must stay backward compatible; existing `EnvelopePayload` fields (type, version, session_id, module_id, timestamp, payload) must not be renamed or removed
- No protobuf breaking changes without a reservation + migration note
- `TransportClass` enum values must match `ClassA`/`ClassB`/`ClassC`/`ClassD` from the constitution

## Acceptance Gates

- [ ] `EnvelopeValidator` rejects envelopes missing `type` or `version`
- [ ] `CorrelationId` and `CausationId` survive a SignalR hub round-trip
- [ ] All proto files pass `protoc --lint` (or `buf lint`)
- [ ] Existing `dotnet test` suite still passes after envelope extension
- [ ] 9 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Contracts/EnvelopePayload.cs` | Base envelope to extend |
| `src/core/MLS.Core/Transport/` | New transport type enums |
| `src/core/MLS.Core/Constants/MessageTypes.cs` | Add transport event constants |
| `src/proto/` | Create proto package hierarchy here |
| `docs/bcg/session-04-extended-document.md` | Authoritative constitution |
| `.prompts-update/BCG_Session_04_Extended_Document.md` | Full session spec |
