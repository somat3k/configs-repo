# Universal Tensor Foundation Note

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active — Tensor-First Mandate
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## 1. Purpose

This document establishes the tensor-first mandate for the BCG ecosystem. It defines what a tensor is within BCG, why tensors govern the advanced execution future of the system, what the universal tensor structure must contain, and what rules govern tensor behavior across module boundaries.

This note is a governance document. It is not a code specification. Code specifications for the tensor contract are produced in Session 03.

---

## 2. Why Tensors Govern the Future System

The BCG system handles diverse workloads: ML inference, training, vector embeddings, scientific compute, mathematical transformation, graph-to-data projection, streaming structured output, batch execution, and hardware-aware optimization. These workloads have historically been served by isolated subsystems using incompatible data structures.

Tensors provide a single unified structure that can represent all of these workload classes:

- an inference input is a float32 tensor of shape [batch, features]
- a training batch is a float32 tensor of shape [batch, seq_len, features]
- an embedding is a float32 tensor of shape [batch, embedding_dim]
- a scientific compute payload is a float64 tensor of arbitrary shape
- a graph adjacency matrix is an int32 or float32 tensor of shape [nodes, nodes]
- a streaming output chunk is a tensor slice with a stream sequence marker
- a batch of ONNX inputs is a named tensor map

By adopting a universal tensor structure, BCG gains the ability to:

1. route computation across modules without format conversion
2. trace data lineage through multi-module pipelines
3. apply hardware-aware optimizations at module boundaries
4. cache hot tensors in Redis without per-module serialization logic
5. externalize large tensors to IPFS with a uniform reference structure
6. validate tensor shapes and dtypes at boundary enforcement points
7. audit every transformation that changes a tensor

The purpose is not to force all logic into one ML library. The purpose is to give the system a universal high-performance data structure that can move across module boundaries with traceability and consistent semantics.

---

## 3. Universal Tensor Structure

Every tensor in BCG that crosses a module boundary or participates in an advanced execution lane must carry the following fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | GUID | Yes | Unique identifier for this tensor instance |
| `dtype` | TensorDtype enum | Yes | Element data type (float32, float16, float64, int32, int64, bool, string) |
| `shape` | int[] | Yes | Dimension sizes in order (e.g., [1, 7]) |
| `payload` | byte[] or null | Conditional | Inline binary payload for small/medium tensors |
| `artifactRef` | string or null | Conditional | IPFS content-addressed reference for large tensors |
| `originModuleId` | GUID | Yes | Module that created this tensor |
| `originBlockId` | GUID or null | No | Block within the module that created this tensor |
| `timestamp` | DateTimeOffset | Yes | UTC creation time |
| `traceId` | string | Yes | Distributed trace identifier (propagated from envelope) |
| `tensorClass` | TensorClass enum | Yes | Classification of this tensor's purpose |
| `tags` | string[] | No | Arbitrary labels for routing, filtering, and observability |
| `lineage` | LineageMarker[] | No | Ordered list of transformations applied to this tensor |
| `persistenceHandle` | string or null | No | Redis key or IPFS CID if this tensor is persisted |

**Rule**: Either `payload` or `artifactRef` must be non-null. A tensor with both null is invalid.

---

## 4. Tensor Classes

BCG defines the following tensor classes. Every tensor crossing a major module boundary must declare its class.

| Class | Purpose |
|-------|---------|
| `Control` | Carries orchestration signals, routing hints, or policy state |
| `Inference` | Input to or output from an ML inference operation |
| `Embedding` | Vector embedding produced by an encoder or embedding model |
| `Artifact` | Binary artifact payload (model weights, serialized graph, etc.) |
| `Stream` | A chunk in a streaming output sequence, carries sequence position |
| `Transformed` | Output of a DataEvolution or schema transformation pipeline |
| `LineagePreserving` | Any tensor where lineage must be preserved through further transformations |
| `Scientific` | Data for mathematical or scientific compute pipelines |
| `Training` | A training batch or training output tensor |
| `Batch` | A grouped set of inference or compute inputs |

---

## 5. Dtype Catalogue

| Dtype | Description | Use Cases |
|-------|-------------|-----------|
| `float32` | 32-bit IEEE 754 float | ML inference, trading signals, most production paths |
| `float16` | 16-bit half-precision float | GPU-optimized inference, memory-constrained paths |
| `bfloat16` | 16-bit brain float | Training on accelerators |
| `float64` | 64-bit double | Scientific compute, high-precision financial math |
| `int32` | 32-bit signed integer | Graph indices, classification outputs, token IDs |
| `int64` | 64-bit signed integer | Large index spaces, token sequences |
| `bool` | Boolean | Masks, attention patterns, gate signals |
| `string` | UTF-8 string tensor | Text generation outputs, code tokens |
| `uint8` | 8-bit unsigned integer | Raw byte payloads, image data |

---

## 6. Shape Discipline

Shape discipline is mandatory for stable execution lanes.

**Rules**:

1. Every tensor crossing a module boundary must declare its full shape before transmission.
2. Dynamic shapes are allowed only when transmitted through controlled signature envelopes that declare the dimension that is dynamic.
3. Shape mismatches at a boundary enforcement point must be rejected with a typed error before processing begins.
4. Batch dimensions must be explicit. A tensor of shape `[7]` is a single sample; `[1, 7]` is a batch of one sample.
5. Shape changes due to transformation must produce a new tensor with updated shape and a lineage marker.
6. No module may silently reshape a tensor without appending a lineage entry.

---

## 7. Lineage Rules

**Lineage Markers**

Each lineage marker records:

- `stepId` (GUID) — unique identifier for this transformation step
- `operationType` — the named operation applied (e.g., `normalize`, `embed`, `reshape`, `slice`)
- `moduleId` — the module that applied the transformation
- `blockId` — the block within that module (if applicable)
- `inputShape` — shape before this transformation
- `outputShape` — shape after this transformation
- `timestamp` — UTC time of transformation
- `traceId` — propagated trace identifier

**Lineage Rules**

1. Any transformation that changes dtype, shape, or payload must append a lineage marker.
2. A tensor that passes through a module without transformation does not require a new lineage entry.
3. Tensor mutation is forbidden unless the operation is explicitly declared as a governed mutation step. Governed mutations must append a lineage marker with `operationType = "mutation"`.
4. Lineage arrays are append-only. No historical lineage marker may be modified or removed.
5. The full lineage array must be preserved across IPFS externalization and retrieval.

---

## 8. Storage Threshold Routing

Tensors are routed to different storage tiers based on their serialized size.

| Size | Storage Tier | Policy |
|------|-------------|--------|
| < 64 KB | Inline in envelope payload | Allowed for control, inference, embedding tensors |
| 64 KB – 10 MB | Redis hot cache | Required for frequently accessed inference results; TTL: 5 minutes default |
| 10 MB – 1 GB | IPFS with PostgreSQL reference | Required for training artifacts, large embeddings, batch results |
| > 1 GB | IPFS with PostgreSQL reference and chunked streaming | Required with chunked transfer; single-envelope delivery prohibited |

These thresholds are the Session 01 baseline. Session 10 refines the complete storage governance matrix.

---

## 9. Tensor Boundary Enforcement Points

The following are mandatory tensor boundary enforcement points:

1. **Module ingress** — Every module that declares a tensor contract must validate incoming tensors at its API or SignalR hub before processing.
2. **Block socket connection** — When two blocks are connected in a composition graph, socket type compatibility (including tensor class and dtype) must be verified at connection time.
3. **Transport egress** — Tensors above the storage threshold must be externalized before transmission. The envelope payload must carry the artifact reference, not the inline tensor payload.
4. **Lineage checkpoint** — Before any transformation operation, the current lineage array is copied to the output tensor. The transformation appends its marker to the copy.

---

## 10. Tensor Naming Conventions

| Context | Convention |
|---------|-----------|
| Inference input | `input_{name}` (e.g., `input_features`) |
| Inference output | `output_{name}` (e.g., `output_logits`) |
| Embedding output | `embedding_{model}` (e.g., `embedding_code`) |
| Training batch | `batch_{split}` (e.g., `batch_train`) |
| Streaming chunk | `stream_{sequence}_{traceId}` |
| Transformed output | `evolved_{sourceId}` |

Naming is enforced by the tensor contract of each module and block.

---

## 11. Tensor Governance Commitment

From Session 01 onward:

1. No advanced execution lane may be added to any module without a declared tensor contract.
2. No inference, training, embedding, or transformation flow may use an untyped generic payload where a tensor structure is applicable.
3. Every new module species declared after Session 01 must include a tensor contract in its 20-document species pack.
4. The Session 03 universal tensor spec will formalize the C# type definitions, ONNX integration rules, and Redis serialization format based on this foundation note.

---

## 12. Future Refinements

This foundation note will be extended by:

- **Session 03**: Universal Tensor Contract and Tensor Lineage (C# types, ONNX integration, cache key design)
- **Session 04**: Tensor serialization in transport envelopes (protobuf schemas, MessagePack wire format)
- **Session 08**: TensorTrainer-specific tensor classes (training batch, checkpoint, export formats)
- **Session 09**: ML Runtime hybrid serving tensor signatures (input/output metadata contracts)
- **Session 10**: Storage governance matrix (precise size thresholds, TTL policies, IPFS chunking)
