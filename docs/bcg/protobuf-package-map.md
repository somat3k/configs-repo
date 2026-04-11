# Protobuf Package Map
## BCG Session 04 — Package Ownership and Schema Registration

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, session-04-extended-document.md

---

## 1. Purpose

This document maps every reserved `bcg.*` protobuf package to its owning module, declares the current schema state, and defines the review gate that governs field evolution within each package.

All proto files live under `src/proto/bcg/<package>/`.

---

## 2. Reserved Package Registry

| Package | Proto File | Owning Module | Status | Schema Version |
|---------|-----------|---------------|--------|----------------|
| `bcg.module` | `src/proto/bcg/module/module.proto` | block-controller | Active | 1 |
| `bcg.block` | `src/proto/bcg/block/block.proto` | block-controller | Active | 1 |
| `bcg.tensor` | `src/proto/bcg/tensor/tensor.proto` | ml-runtime / data-layer | Active | 1 |
| `bcg.session` | `src/proto/bcg/session/session.proto` | web-app / shell-vm | Active | 1 |
| `bcg.observability` | `src/proto/bcg/observability/observability.proto` | block-controller | Active | 1 |
| `bcg.orchestrator` | Reserved — Session 05+ | block-controller | Reserved | — |
| `bcg.transform` | Reserved — DataEvolution | data-layer | Reserved | — |
| `bcg.runtime` | Reserved — ML Runtime kernels | ml-runtime | Reserved | — |
| `bcg.training` | Reserved — TensorTrainer | ml-runtime | Reserved | — |
| `bcg.storage` | Reserved — Storage routing | data-layer | Reserved | — |

---

## 3. Field Evolution Policy Per Package

### `bcg.module`
- **Owning module**: block-controller
- **Approved additions**: optional fields for new capability dimensions, new health event subtypes
- **Prohibited**: changing `module_id` field number, removing `RegisterModule.capabilities`
- **Review gate**: block-controller maintainer + architecture review for any breaking change

### `bcg.block`
- **Owning module**: block-controller
- **Approved additions**: optional execution hints, new stream fragment metadata fields
- **Prohibited**: changing `block_id` semantics, changing `BlockRequest.trace_id` field number
- **Review gate**: block-controller maintainer

### `bcg.tensor`
- **Owning module**: ml-runtime / data-layer (joint)
- **Approved additions**: new layout types as optional fields, new dtype enum values (forward-safe)
- **Prohibited**: changing `tensor_id`, `dtype`, or `shape` field numbers
- **Review gate**: ml-runtime + data-layer joint review for any dtype or shape change

### `bcg.session`
- **Owning module**: web-app / shell-vm (joint)
- **Approved additions**: new session state types, new operator control actions
- **Prohibited**: changing `session_id` or `operator_id` field numbers
- **Review gate**: web-app maintainer

### `bcg.observability`
- **Owning module**: block-controller
- **Approved additions**: new metric fields, new route event subtypes
- **Prohibited**: changing `trace_id` or `route_event_type` field numbers
- **Review gate**: block-controller maintainer

---

## 4. Schema Promotion Gate

A schema cannot be promoted from draft to production-grade unless all of the following pass:

1. **Protobuf lint** — `buf lint` passes with zero warnings under BCG lint rules
2. **Semantic review** — owning module maintainer explicitly approves semantic meaning of all fields
3. **Contract tests** — round-trip serialization tests pass for all message types in the schema
4. **Compatibility notes** — compatibility mode declared (backward / forward / breaking)
5. **Failure mode description** — all rejection and error paths documented
6. **Representative sample payloads** — at least one sample payload per message type included in the PR

---

## 5. Reserved Field Number Ranges

To avoid accidental collision across packages, field numbers are reserved by range:

| Range | Assignment |
|-------|-----------|
| 1–99 | Core identity fields (IDs, type, version, timestamp) |
| 100–199 | Operational fields (capabilities, health, routing hints) |
| 200–299 | Payload and data fields |
| 300–399 | Lineage and trace fields |
| 400–499 | Integrity and security fields |
| 500+ | Extension and future fields |

Within each package, field numbers must be monotonically assigned. Deleted fields must be reserved with a comment explaining the deletion reason and the version in which they were removed.

---

## 6. Enum Forward-Safety Rule

All enum types in BCG proto packages must include an `UNSPECIFIED = 0` value as the first entry. This ensures that unknown future enum values from newer senders are tolerated by older consumers as the zero/default value rather than causing parse failures.

Example:
```proto
enum TransportClass {
  TRANSPORT_CLASS_UNSPECIFIED = 0;
  CLASS_A = 1;
  CLASS_B = 2;
  CLASS_C = 3;
  CLASS_D = 4;
}
```

---

## 7. Package Naming Convention

All message types must use `PascalCase`. Field names must use `snake_case`. Service names use `PascalCase` with a `Service` suffix. RPC method names use `PascalCase` verbs.

Proto file `option go_package`, `option csharp_namespace`, and `option java_package` must be set in all production proto files to ensure clean code generation in each target language.
