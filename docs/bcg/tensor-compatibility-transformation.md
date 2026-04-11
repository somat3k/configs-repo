# Tensor Compatibility and Transformation Rules
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `shape-dtype-governance.md` · `src/core/MLS.Core/Tensor/ITensorContract.cs`
> **Status**: ✅ Frozen for Phase 0

---

## 1. Compatibility Classes

Every module must declare the tensor types it accepts by implementing `ITensorContract`. The contract declares which combination of dtype, shape class, and layout the module handles natively.

| Declared Class | Meaning |
|---------------|---------|
| Exact dtype only | Module accepts a single specific dtype (e.g. only `Float32`) |
| Exact shape only | Module accepts only tensors matching a specific static shape |
| Bounded dynamic shape | Module accepts a declared set of shape classes including `BoundedDynamic` |
| Semantic dtype family | Module accepts a group of dtypes sharing a semantic family (e.g. all numeric types) |
| Sparse/Ragged forms | Module accepts `TensorLayout.Sparse` or `TensorLayout.Ragged` inputs |
| Artifact-backed tensors | Module can consume `TensorLayout.ArtifactBacked` (fetch from reference) |
| Graph-derived tensors | Module accepts `TensorLayout.GraphDerived` inputs |

---

## 2. Compatibility Check Flow

Before dispatching a tensor to a target module, the Block Controller governor:

1. Resolves the target module's registered `ITensorContract`
2. Calls `contract.IsCompatible(tensor)`
3. If compatible → dispatch inline or by reference
4. If not compatible → invoke the transformation bus
5. If the transformation bus cannot produce a compatible form → emit `TENSOR_COMPATIBILITY_ERROR`

```
Incoming tensor
      │
      ▼
ITensorContract.IsCompatible(tensor)?
      │ YES                         │ NO
      ▼                             ▼
Dispatch to target module    Transformation Bus
                                    │ legal path?
                       YES ─────────┤
                        ▼           │ NO
                  Transformed       ▼
                  tensor       TENSOR_COMPATIBILITY_ERROR
                  dispatched   (route fails explicitly)
```

---

## 3. Transformation Bus Responsibilities

The transformation bus is the **only lawful place** for the following operations:

| Operation | Example |
|-----------|---------|
| Explicit cast | Float64 → Float32 (with lineage record, lossy flag if applicable) |
| Reshape | `[1, 7]` → `[7]` |
| Broadcast | `[1, 128]` → `[32, 128]` |
| Slice | Extract sub-tensor along an axis |
| Pad | Extend dimensions with zero or fill values |
| Sparse ↔ Dense conversion | COO sparse → dense float array |
| Graph-to-tensor adaptation | Graph adjacency → dense float matrix |
| Semantic normalization | Normalize token stream → embedding vector |

**It is not lawful** for target modules to:
- Internally coerce a received dtype without the transformation bus
- Silently reshape a tensor without creating lineage
- Reinterpret a semantic dtype (e.g. treat `CodeTokenStream` as raw `Bytes`) without updating `meta.semantic_tag`

---

## 4. Failure Behavior

When a tensor is incompatible and no legal transformation exists:

1. **The route must fail explicitly.** The Block Controller emits `TENSOR_COMPATIBILITY_ERROR` with a `TensorCompatibilityErrorPayload`.
2. **The failure must emit a typed compatibility error.** The error payload includes: tensor ID, trace ID, producer module, target module, dtype, shape class, layout, and `incompatibility_reason`.
3. **Lineage and telemetry must record the failure cause.** The `compatibility_notes` field in the lineage and the observability event must reference the failure reason.
4. **No fallback silent reinterpretation is allowed.** The route does not proceed with a best-effort interpretation.

---

## 5. Transformation Bus and Lineage

Every transformation performed by the bus creates a new `BcgTensor` via `parent.Derive(...)` and emits:
- `TENSOR_TRANSFORMED` with `TensorTransformedPayload`
- `TENSOR_LINEAGE_CREATED` with `TensorLineageCreatedPayload`

The transformation step ID must be a stable, versioned identifier (e.g. `"cast:float64-to-float32:v1"`).

---

## 6. Future Module Contracts

### 6.1 TensorTrainer

**Accepted inputs:**
- `TensorLayout.Dense` with `TensorDType.Float32` or `Float64`
- `TensorLayout.Sequence` for text/code training corpora
- `TensorLayout.GraphDerived` from transformed graph structures
- `TensorLayout.ArtifactBacked` for large dataset archives

**Emitted outputs:**
- `TensorLayout.ArtifactBacked` → trained model artifact reference (IPFS)
- `TensorLayout.Dense` → evaluation metric tensors
- `TensorLayout.Dense` → quality and loss tensors with lineage back to the dataset

### 6.2 DataEvolution

**Accepted inputs:**
- Any source form — arbitrary dtype, layout, and encoding
- Normalization contract must be declared per source adapter

**Emitted outputs:**
- `TensorLayout.Dense` canonical normalized tensors
- `TensorLayout.GraphDerived` for communication uplift use cases
- `TensorDType.EmbeddingVector` for semantic tensors usable by downstream runtime lanes
- All outputs carry full lineage from the source data form through normalization steps

DataEvolution is therefore one of the strictest consumers of the universal tensor contract — it must track lineage from raw source to canonical form.
