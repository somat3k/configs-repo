# Session 03 Extended Document
## Universal Tensor Contract, Tensor Lineage, and Cross-Module Compatibility Constitution

> **Status**: ✅ Complete — All deliverables implemented and verified.
> **Phase**: 0 — Foundation
> **Depends on**: Session 02 (Block Controller Runtime Governor)
> **Produces**: Universal tensor contract, C# core types, governance documents

---

## 1. Session Purpose

Session 03 defines the universal tensor contract for the BCG ecosystem. This session is the legal and technical foundation for how advanced data moves, mutates, persists, streams, composes, and remains traceable across the Block Controller network.

Session 02 elevated the Block Controller into the runtime governor. Session 03 now defines the primary governed object that the runtime must recognize, schedule, route, cache, transform, and observe: **the tensor**.

This session is not limited to machine learning. The tensor contract must serve:

- inference
- training
- embeddings
- scientific and mathematical compute
- code intelligence
- text generation
- graph communication uplift
- stream normalization
- block-to-block execution
- multi-module data evolution

The output of this session is a production-grade constitution for tensor identity, structure, transport, lineage, compatibility, and storage responsibility.

---

## 2. Strategic Mandate

The BCG platform is moving from message-led service choreography toward **tensor-governed execution**. The existing repo defines the wider distributed conditions: a Block Controller orchestration hub, dynamic specialist modules, Blazor MDI operator surface, and a persistence layer spread across PostgreSQL, Redis, and IPFS. The system-architect skill also defines a central orchestration topology, typed envelope discipline, and self-reporting modules in a distributed network.

Session 03 strengthens that model by declaring that any advanced inter-module value must be representable as a governed tensor object, even when its original source is not numeric. Text, code, serialized graphs, vectors, sparse structures, transformed event streams, and scientific matrices must all be able to enter the platform under one controlled execution model.

This is aligned with TensorFlow's core execution logic. TensorFlow's `tf.function` creates compiled graph-backed callables, specializes behavior based on argument types and shapes, and explicitly warns that uncontrolled retracing is a major performance concern. Stable signatures and controlled shapes are therefore part of production correctness, not just optimization.

---

## 3. Session Objectives

### Primary Objectives
- ✅ Define the canonical tensor structure for BCG
- ✅ Define tensor identity and traceability rules
- ✅ Define tensor shape and dtype policy
- ✅ Define transport class policy for tensor movement
- ✅ Define storage routing rules between Redis, PostgreSQL, and IPFS
- ✅ Define tensor lineage and mutation history
- ✅ Define compatibility rules across modules and execution lanes
- ✅ Define production QA and certification gates for tensor-compliant modules

### Secondary Objectives
- ✅ Prepare the DataEvolution module contract for future arbitrary-source normalization
- ✅ Prepare the TensorTrainer module contract for high-compute tensor-native pipelines
- ✅ Prepare ML Runtime and runtime kernels for stable shape-aware flows
- ✅ Create a future-proof basis for graph-to-tensor and stream-to-tensor composition

---

## 4. Session Deliverables

| # | Deliverable | Document | C# Type |
|---|-------------|----------|---------|
| 1 | Universal tensor contract | `universal-tensor-contract.md` | `BcgTensor`, `ITensorContract` |
| 2 | Tensor lifecycle model | `tensor-lifecycle.md` | — |
| 3 | Tensor lineage model | `tensor-lineage.md` | `TensorLineageRecord` |
| 4 | Shape and dtype governance policy | `shape-dtype-governance.md` | `TensorDType`, `TensorShapeClass` |
| 5 | Tensor transport matrix | `tensor-transport-matrix.md` | `TensorTransportClass` |
| 6 | Tensor storage threshold policy | `tensor-storage-threshold.md` | `TensorStorageMode`, `TensorPersistenceRef` |
| 7 | Tensor compatibility and transformation rules | `tensor-compatibility-transformation.md` | `ITensorContract` |
| 8 | Observability requirements | `observability-tensor.md` | Payload contracts |
| 9 | QA gates for tensor-compliant modules | `tensor-qa-gates.md` | `TensorCertificationLevel` |
| 10 | Module charter addenda | Session docs (per module) | — |

---

## 5. Implementation Record

### C# Types Delivered (`src/core/MLS.Core/Tensor/`)

| File | Description |
|------|-------------|
| `BcgTensor.cs` | Canonical tensor record with `CreateRoot`, `Derive`, `ElementCount`, `IsRoot` |
| `TensorDType.cs` | Enum: Float32, Float64, Int32, Int64, UInt8, Bool, String, Bytes, JsonSemantic, EmbeddingVector, CodeTokenStream, GraphPacket |
| `TensorLayout.cs` | Enum: Dense, Sparse, Ragged, Sequence, GraphDerived, ArtifactBacked |
| `TensorShapeClass.cs` | Enum: ExactStatic, BoundedDynamic, RaggedStructured, SparseStructured, GraphDerivedVariable |
| `TensorTransportClass.cs` | Enum: Inline, Stream, Reference |
| `TensorStorageMode.cs` | Enum: Transient, Redis, Postgres, Ipfs |
| `TensorEncoding.cs` | Enum: RawFloat32LE, RawFloat64LE, …, JsonUtf8, MessagePack, Utf8String, OpaqueBytes |
| `TensorCertificationLevel.cs` | Enum: TensorNative, CompatibleViaTransformationBus, ReadOnlyConsumer, ProducingSpecialist, NotCertified |
| `TensorMeta.cs` | Record: origin, trace, session, tags, contract version, semantic tag, confidence |
| `TensorLineageRecord.cs` | Record with `Create` factory; operations list, lossy cast flag |
| `TensorPersistenceRef.cs` | Record: Redis key, Postgres ID, IPFS CID, storage mode, expiry |
| `TensorIntegrity.cs` | Record: payload hash, contract fingerprint, optional signature |
| `ITensorContract.cs` | Interface for module input/output contract declaration and compatibility check |

### Message Types (`src/core/MLS.Core/Constants/MessageTypes.Tensor.cs`)

| Constant | Value |
|----------|-------|
| `TensorValidationFailed` | `"TENSOR_VALIDATION_FAILED"` |
| `TensorRouted` | `"TENSOR_ROUTED"` |
| `TensorTransformed` | `"TENSOR_TRANSFORMED"` |
| `TensorStored` | `"TENSOR_STORED"` |
| `TensorLineageCreated` | `"TENSOR_LINEAGE_CREATED"` |
| `TensorCompatibilityError` | `"TENSOR_COMPATIBILITY_ERROR"` |
| `TensorAccepted` | `"TENSOR_ACCEPTED"` |
| `TensorBatchComplete` | `"TENSOR_BATCH_COMPLETE"` |

### Contract Payloads (`src/core/MLS.Core/Contracts/Tensor/`)

| File | Message Type |
|------|-------------|
| `TensorValidationFailedPayload.cs` | `TENSOR_VALIDATION_FAILED` |
| `TensorCompatibilityErrorPayload.cs` | `TENSOR_COMPATIBILITY_ERROR` |
| `TensorRoutedPayload.cs` | `TENSOR_ROUTED` |
| `TensorTransformedPayload.cs` | `TENSOR_TRANSFORMED` |
| `TensorStoredPayload.cs` | `TENSOR_STORED` |
| `TensorLineageCreatedPayload.cs` | `TENSOR_LINEAGE_CREATED` |

### Tests

`src/core/MLS.Core.Tests/Tensor/BcgTensorTests.cs` — 22 tests covering:
- Root tensor creation invariants
- `ElementCount` for static and dynamic shapes
- `Derive` lineage accumulation across multiple generations
- `TensorLineageRecord.Create` factory
- `TensorPersistenceRef.IsExternalized` for all storage modes

**Test results**: 36 / 36 passed (14 pre-existing + 22 new).

---

## 6. Skills Applied

- `.skills/dotnet-devs.md` — primary constructors, records, C# 13
- `.skills/machine-learning.md` — tensor types, ONNX, shape discipline
- `.skills/storage-data-management.md` — Redis / Postgres / IPFS routing policy
- `.skills/websockets-inferences.md` — transport class matrix
- `.skills/system-architect.md` — envelope discipline, module contracts
- `.skills/beast-development.md` — immutable records, zero-alloc direction

---

## 7. Session 03 Final Statement

Session 03 makes the tensor the lawful execution object of the BCG ecosystem. From this point forward, advanced values inside the fabric are not treated as anonymous payloads. They are governed, shaped, typed, traceable, persistent when necessary, transformable only through explicit law, and observable across their entire lifetime.

The platform can now move into later sessions with a stable basis for runtime kernels, batch containers, training species, transformation species, and cross-module graph intelligence without degenerating into incompatible ad hoc payload handling.
