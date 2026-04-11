# Shape and DType Governance Policy
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `src/core/MLS.Core/Tensor/TensorDType.cs` · `src/core/MLS.Core/Tensor/TensorShapeClass.cs`
> **Status**: ✅ Frozen for Phase 0

---

## 1. DType Governance

### 1.1 Core Rule

DType is not only a serialisation detail. It is part of **execution legality**. No hidden casting is permitted in production routes. Any cast must be explicit, observable, and lineage-preserving.

### 1.2 Required DType Families

| Enum Value | Physical | Use |
|------------|----------|-----|
| `Float32` | 4-byte IEEE 754 | Model I/O, feature vectors, default for ONNX |
| `Float64` | 8-byte IEEE 754 | Scientific compute, high-precision indicators |
| `Int32` | 4-byte signed int | Indices, counts, integer features |
| `Int64` | 8-byte signed int | Timestamps as Unix epoch, large counts |
| `UInt8` | 1-byte unsigned | Quantised weights, image data |
| `Bool` | 1-byte boolean | Gate decisions, masks |
| `String` | UTF-8 bytes | Text payloads, metadata |
| `Bytes` | Opaque binary | Raw blobs, serialised structures |
| `JsonSemantic` | UTF-8 JSON | Structured semantic payloads with schema identity |
| `EmbeddingVector` | Float32 bytes | Dense embeddings from text/image/code models |
| `CodeTokenStream` | UTF-8 / bytes | Tokenised source, AST fragments |
| `GraphPacket` | Bytes / JSON | Serialised graph topology or incidence structure |

### 1.3 DType Rules

1. **No hidden casting in production routes.** A `Float64` tensor must not be silently truncated to `Float32` during routing.
2. **Any cast must be explicit, observable, and lineage-preserving.** Casts must be performed by the transformation bus and recorded in `TensorLineageRecord.operations`.
3. **Lossy casts require policy approval.** Lossy casts (Float64 → Float32, Int64 → Int32) must set `is_lossy_cast = true` in the lineage record.
4. **Module input contracts must declare accepted dtypes.** Modules declare `ITensorContract.AcceptedInputDTypes`.
5. **Fallback casting is only lawful in the transformation bus.** Target modules must not coerce dtypes internally without going through the bus.

### 1.4 Semantic DTypes

Certain payloads are semantically special even if they share a physical encoding with other types:

| Semantic DType | Physical Encoding | Semantic Tag Required |
|---------------|-------------------|----------------------|
| `EmbeddingVector` | Float32 bytes | Model name + embedding dimension |
| `CodeTokenStream` | UTF-8 bytes | Vocabulary name + tokeniser version |
| `GraphPacket` | Bytes or JSON | Graph schema version |
| `JsonSemantic` | UTF-8 JSON | JSON schema identifier |

The `meta.semantic_tag` field must be populated for these types so downstream modules know the legal interpretation.

---

## 2. Shape Governance

### 2.1 Shape is a Runtime Constraint

TensorFlow's execution model makes shape discipline essential because shape variance contributes to retracing and concrete-function multiplication. BCG follows the same principle: **shape policy is part of runtime stability, not just a serialisation hint**.

### 2.2 Shape Rules

1. **Shapes must be declared on tensor creation.** An empty or null shape is not valid for production tensors (exception: scalar tensors may use `[]`).
2. **Dynamic dimensions are allowed only where the module contract permits them.** A `-1` value in `shape` requires the module to declare `BoundedDynamic` or higher in `AcceptedInputShapeClasses`.
3. **Uncontrolled unknown rank is forbidden in production lanes.** Rank-0 unknown is not a valid shape class in any lane.
4. **Tensors entering compiled or batch-optimised lanes must obey static or bounded dynamic shape policy.** Variable-rank tensors must be directed to appropriate lanes by the governor.
5. **Modules must declare shape tolerance classes** via `ITensorContract.AcceptedInputShapeClasses`.

### 2.3 Shape Classes

| Enum Value | Meaning | Lane Requirements |
|------------|---------|------------------|
| `ExactStatic` | All dimensions known at creation | Required for compiled/batch-optimised lanes |
| `BoundedDynamic` | One or more axes vary within a declared upper bound | Allowed in flexible lanes; bound must be declared |
| `RaggedStructured` | Row lengths vary | Only in lanes that explicitly declare ragged support |
| `SparseStructured` | Non-zero density declared; dimensions may be large | Only in sparse-aware lanes |
| `GraphDerivedVariable` | Shape determined by graph topology | Only in graph-aware lanes |

### 2.4 Shape Enforcement by the Governor

The Block Controller routing governor may reject, reroute, or invoke transformation when:

| Condition | Governor Action |
|-----------|----------------|
| Shape class incompatible with target kernel | Attempt transformation bus; if none, emit `TENSOR_COMPATIBILITY_ERROR` |
| Shape exceeds lane budget (max element count) | Reroute to reference transport; emit `TENSOR_STORED` |
| Shape would break static-batch optimization | Reject from batch lane; route to flexible lane |
| Shape would cause unbounded retracing risk | Reject; require explicit dynamic-shape contract declaration |

### 2.5 Shape Summary Notation

The `shape_summary` field in observability payloads uses the notation `"[d0, d1, …]"` with `-1` for dynamic axes:

```
"[1, 7]"          — static dense inference input
"[-1, 128]"       — dynamic batch, 128-dim embedding
"[32, -1]"        — batch of 32, variable sequence length
"[]"              — scalar
```
