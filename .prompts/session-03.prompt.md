---
mode: agent
description: "BCG Session 03 — Universal Tensor Contract and Tensor Lineage"
status: "✅ Complete — C# types and 10 governance documents exist"
depends-on: ["session-01", "session-02"]
produces: ["docs/bcg/tensor-*.md", "src/core/MLS.Core/Tensor/", "src/core/MLS.Core/Contracts/Tensor/"]
---

# Session 03 — Universal Tensor Contract and Tensor Lineage

> **Status**: ✅ Complete — `src/core/MLS.Core/Tensor/` contains all C# types, 10 docs exist in `docs/bcg/`.  
> Run this prompt to verify completeness, extend coverage, or fix any gaps.

## Session Goal

Define the canonical tensor structure, identity, lineage, transport, storage routing, and compatibility rules as the governed execution object for all advanced inter-module flows.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [x] `session-03-extended-document.md`
- [x] `universal-tensor-contract.md`
- [x] `tensor-lifecycle.md`
- [x] `tensor-lineage.md`
- [x] `shape-dtype-governance.md`
- [x] `tensor-transport-matrix.md`
- [x] `tensor-storage-threshold.md`
- [x] `tensor-compatibility-transformation.md`
- [x] `observability-tensor.md`
- [x] `tensor-qa-gates.md`

### C# Types (`src/core/MLS.Core/Tensor/`)
- [x] `BcgTensor.cs` — `CreateRoot`, `Derive`, `ElementCount`, `IsRoot`
- [x] `TensorDType.cs` — Float32, Float64, Int32, Int64, UInt8, Bool, String, Bytes, JsonSemantic, EmbeddingVector, CodeTokenStream, GraphPacket
- [x] `TensorLayout.cs` — Dense, Sparse, Ragged, Sequence, GraphDerived, ArtifactBacked
- [x] `TensorShapeClass.cs` — ExactStatic, BoundedDynamic, RaggedStructured, SparseStructured, GraphDerivedVariable
- [x] `TensorTransportClass.cs` — Inline, Stream, Reference
- [x] `TensorStorageMode.cs` — Transient, Redis, Postgres, Ipfs
- [x] `TensorEncoding.cs` — RawFloat32LE, RawFloat64LE, JsonUtf8, MessagePack, Utf8String, OpaqueBytes, etc.
- [x] `TensorCertificationLevel.cs` — TensorNative, CompatibleViaTransformationBus, ReadOnlyConsumer, ProducingSpecialist, NotCertified
- [x] `TensorMeta.cs` — origin, trace, session, tags, contract version, semantic tag, confidence
- [x] `TensorLineageRecord.cs` — `Create` factory, operations list, lossy cast flag
- [x] `TensorPersistenceRef.cs` — Redis key, Postgres ID, IPFS CID, storage mode, expiry
- [x] `TensorIntegrity.cs` — payload hash, contract fingerprint, optional signature
- [x] `ITensorContract.cs` — module input/output contract declaration and compatibility check

### Contract Payloads (`src/core/MLS.Core/Contracts/Tensor/`)
- [x] `TensorValidationFailedPayload.cs`
- [x] `TensorCompatibilityErrorPayload.cs`
- [x] `TensorRoutedPayload.cs`
- [x] `TensorTransformedPayload.cs`
- [x] `TensorStoredPayload.cs`
- [x] `TensorLineageCreatedPayload.cs`

### Message Types (`src/core/MLS.Core/Constants/MessageTypes.Tensor.cs`)
- [x] `TENSOR_VALIDATION_FAILED`, `TENSOR_ROUTED`, `TENSOR_TRANSFORMED`, `TENSOR_STORED`
- [x] `TENSOR_LINEAGE_CREATED`, `TENSOR_COMPATIBILITY_ERROR`, `TENSOR_ACCEPTED`, `TENSOR_BATCH_COMPLETE`

### Tests (`src/core/MLS.Core.Tests/Tensor/`)
- [x] `BcgTensorTests.cs` — 22 tests: root creation, ElementCount, Derive lineage, TensorLineageRecord, TensorPersistenceRef

### Remaining / Extension Tasks
- [ ] Verify all 36 tests still pass: `dotnet test src/core/MLS.Core.Tests/`
- [ ] Confirm `universal-tensor-foundation.md` references `EnvelopePayload` and Session 04 trace extensions
- [ ] Add `TensorAcceptedPayload.cs` if missing in Contracts/Tensor/
- [ ] Add `TensorBatchCompletePayload.cs` if missing in Contracts/Tensor/

## Skills to Apply

```
.skills/machine-learning.md          — tensor types, ONNX, shape discipline
.skills/dotnet-devs.md               — records, C# 13, primary constructors, nullable refs
.skills/storage-data-management.md   — Redis/Postgres/IPFS routing policy
.skills/websockets-inferences.md     — transport class matrix
.skills/system-architect.md          — envelope discipline, module contracts
.skills/beast-development.md         — immutable records, zero-alloc direction
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — tensor events sent via typed EnvelopePayload
- All tensor types in `src/core/MLS.Core/Tensor/` — never in module-local code

## Acceptance Gates

- [ ] `dotnet test src/core/MLS.Core.Tests/` — all tests pass (36+)
- [ ] `ITensorContract` is implemented by at least one module as a type-check
- [ ] `TensorPersistenceRef.IsExternalized` returns true only for Redis/Postgres/IPFS modes
- [ ] All 10 governance documents present in `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Tensor/` | All C# tensor types |
| `src/core/MLS.Core/Contracts/Tensor/` | Typed payload records |
| `src/core/MLS.Core/Constants/MessageTypes.Tensor.cs` | Tensor event constants |
| `src/core/MLS.Core.Tests/Tensor/BcgTensorTests.cs` | Unit tests |
| `.prompts-update/BCG_Session_03_Extended_Document.md` | Full session spec |
