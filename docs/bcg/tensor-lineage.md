# Tensor Lineage Constitution
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `src/core/MLS.Core/Tensor/TensorLineageRecord.cs`
> **Status**: ✅ Frozen for Phase 0

---

## 1. Purpose

Lineage is mandatory for every materially transformed tensor in the BCG platform. Lineage enables:

- **Auditability** — who changed what, and when
- **Replay** — reconstruct any execution from first principles
- **Debugging** — trace a corrupt output back to its origin
- **Scientific reproducibility** — identical inputs must produce identical tensors
- **Model validation** — confirm training data provenance
- **Stream transformation traceability** — follow a value through normalization steps
- **Multi-module causal reconstruction** — understand the full execution path

---

## 2. Lineage Record Structure

Each `TensorLineageRecord` must include:

| Field | Type | Description |
|-------|------|-------------|
| `lineage_id` | `Guid` | Unique record identifier |
| `parent_tensor_ids` | `Guid[]` | One or more parent tensors. Non-empty. |
| `transformation_step_id` | `string` | Stable identifier of the computation step |
| `producing_module_id` | `string` | Module that performed the transformation |
| `producing_block_id` | `Guid?` | Block inside the module (if applicable) |
| `kernel_version` | `string` | Version of the kernel or transform function |
| `timestamp` | `DateTimeOffset` | UTC time of transformation |
| `operations` | `string[]` | Ordered list of operations (e.g. `"reshape:[1,7]→[7]"`) |
| `is_lossy_cast` | `bool` | True when a lossy numeric cast occurred |
| `persistence_relocation_note` | `string?` | Note when payload moved storage tiers |
| `compatibility_notes` | `string?` | Notes about dtype or shape compatibility decisions |

---

## 3. Lineage Rules

### 3.1 No Silent Mutation
A tensor is immutable once published. Any operation that changes dtype, shape, layout, encoding, or semantic interpretation must produce a new tensor using `BcgTensor.Derive(...)`.

### 3.2 Transformed Outputs Become New Tensors
The `Derive` method on `BcgTensor` creates a new tensor with:
- A new `Guid` identity
- `meta.trace_id` inherited from the parent
- The lineage record appended to the `Lineage` list

### 3.3 Lineage Must Survive Storage Relocation
When a tensor payload is moved from Redis to IPFS (or any tier relocation), a lineage record must be appended with `persistence_relocation_note` populated. The tensor ID does not change for a storage relocation — only the `persistence` reference field is updated.

### 3.4 Replay and Audit Reconstruction
Audit and replay systems must be able to reconstruct the full ancestry chain by:
1. Loading the tensor's `lineage` array
2. For each `parent_tensor_ids`, fetching those tensors' lineage arrays recursively
3. Walking back to root tensors (those with empty lineage)

### 3.5 Lineage Must Not Depend Only on Volatile Memory
Every lineage record created in production must be persisted (PostgreSQL recommended) via `TENSOR_LINEAGE_CREATED` envelope before the tensor is dispatched to consumers.

### 3.6 Lossy Casts Must Be Marked
Any cast where the target dtype has lower precision (e.g. Float64 → Float32, Int64 → Int32) must set `is_lossy_cast = true` in the lineage record. Lossy casts require policy approval and must not occur silently in production routes.

---

## 4. Operation Notation

Operations in `lineage.operations` follow a standard string notation:

| Pattern | Example |
|---------|---------|
| Cast | `"cast:float64→float32"` |
| Reshape | `"reshape:[1,7]→[7]"` |
| Pad | `"pad:axis=0,from=5,to=10"` |
| Slice | `"slice:axis=0,start=0,end=32"` |
| Broadcast | `"broadcast:[1,128]→[32,128]"` |
| Sparse→Dense | `"sparse_to_dense"` |
| Dense→Sparse | `"dense_to_sparse"` |
| Normalise | `"normalize:mean=0,std=1"` |
| Storage relocation | `"storage:redis→ipfs"` |

---

## 5. Root Tensor Declaration

Root tensors are born at ingestion or production boundaries. They have:
- Empty `lineage` array
- `BcgTensor.IsRoot == true`

Root tensors must still carry full identity metadata:
- `meta.origin_module_id` (mandatory)
- `meta.trace_id` (mandatory — supplied by the calling context)
- `meta.created_at` (set automatically by `CreateRoot`)

---

## 6. Multi-Parent Lineage

Some tensors are produced by merging or fusing multiple parent tensors (e.g. concatenation, feature fusion). In this case, `parent_tensor_ids` contains multiple entries. The lineage record must list all contributing parents.

```csharp
var fusionLineage = TensorLineageRecord.Create(
    parentTensorIds: [tensorA.Id, tensorB.Id],
    transformationStepId: "feature-fusion",
    producingModuleId: "ml-runtime",
    kernelVersion: "2.1",
    operations: ["concat:axis=1", "normalize:mean=0,std=1"]);
```

---

## 7. Storage Responsibility for Lineage

| Store | Contents |
|-------|----------|
| **PostgreSQL** | All production lineage records — authoritative, permanent |
| **Redis** | Optional hot cache for recent lineage records to accelerate replay |
| **In-memory** | `BcgTensor.Lineage` array — present for the lifetime of the tensor object |

Lineage records must not be held only in-memory. The `TENSOR_LINEAGE_CREATED` envelope must be emitted and the record persisted before the derived tensor is dispatched.
