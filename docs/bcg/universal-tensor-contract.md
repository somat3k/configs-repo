# Universal Tensor Contract
## BCG Platform — Session 03 Governance Document

> **Reference**: `session-03-extended-document.md` · `src/core/MLS.Core/Tensor/BcgTensor.cs`
> **Contract Version**: 1
> **Status**: ✅ Frozen for Phase 0

---

## 1. Foundational Definition

A BCG tensor is the canonical high-value data carrier used by the platform for governed execution. It is not merely a payload buffer. It is a traceable, typed, shape-aware execution object carrying enough metadata to support:

- routing
- validation
- caching
- persistence
- replay
- transformation
- compatibility checks
- provenance reconstruction
- performance attribution

A tensor may wrap a native runtime tensor, a flat data buffer, a sparse representation, a graph-derived structure, or an externalized large artifact reference, provided it obeys the universal contract.

---

## 2. Canonical Identity Fields

Every tensor must carry:

| Field | Type | Description |
|-------|------|-------------|
| `id` | `Guid` | Globally unique tensor identifier. Immutable once published. |
| `dtype` | `TensorDType` | Declared scalar or semantic data type. |
| `shape` | `int[]` | Declared dimensions in row-major order. `-1` for dynamic axes. |
| `layout` | `TensorLayout` | Physical memory layout: Dense, Sparse, Ragged, Sequence, GraphDerived, ArtifactBacked. |
| `shape_class` | `TensorShapeClass` | Shape tolerance class: ExactStatic, BoundedDynamic, RaggedStructured, SparseStructured, GraphDerivedVariable. |
| `transport_class` | `TensorTransportClass` | How the payload moves: Inline, Stream, Reference. |
| `encoding` | `TensorEncoding` | Binary or text format of the data payload. |
| `data` | `JsonElement?` | Inline data payload; null when externalized. |
| `meta` | `TensorMeta` | Origin, trace, session, tags, contract version, semantic tag, confidence. |
| `lineage` | `TensorLineageRecord[]` | Ordered history of transformation steps. Empty for root tensors. |
| `persistence` | `TensorPersistenceRef?` | Storage reference when cached or externalized. |
| `integrity` | `TensorIntegrity?` | Payload hash, contract fingerprint, optional signature. |

---

## 3. Identity Rules

1. **Tensor identity is immutable.** Once a tensor is published beyond the local producing context, its `id`, `dtype`, `shape`, `layout`, and `meta` are frozen.
2. **Material transformations create new tensors.** Any reshape, cast, pad, slice, broadcast, compression change, semantic reinterpretation, or value change that affects downstream behavior must produce a new `BcgTensor` with lineage referencing the prior one.
3. **Trace ID stability.** `meta.trace_id` must remain stable across a request/session lineage. A new root trace ID is generated only when a new top-level process is intentionally created.
4. **Explicit origin.** `meta.origin_module_id` must be populated for every production tensor. `meta.origin_block_id` should be populated when the tensor originates inside a block graph.
5. **Contract version.** `meta.contract_version` must equal `TensorMeta.CurrentContractVersion` (currently `1`).

---

## 4. Tensor Classes

| Class | Layout | Typical Use |
|-------|--------|-------------|
| Scalar / Small Dense | `Dense` | Control signals, thresholds, compact features |
| Dense Numeric | `Dense` | Model inference, science compute, training slices |
| Embedding | `Dense` | Text/image/code embeddings, semantic similarity |
| Sequence | `Sequence` | Token sequences, event windows, temporal data |
| Ragged / Sparse | `Ragged` / `Sparse` | Variable-length structures, graph adjacency |
| Artifact-Backed | `ArtifactBacked` | Large model inputs, snapshots, batch archives |
| Graph-Derived | `GraphDerived` | Graph neighborhood projection, multi-hop matrices |

---

## 5. C# Type Reference

```csharp
// Root creation
var tensor = BcgTensor.CreateRoot(
    dtype: TensorDType.Float32,
    shape: [1, 7],
    layout: TensorLayout.Dense,
    shapeClass: TensorShapeClass.ExactStatic,
    data: jsonElement,
    encoding: TensorEncoding.RawFloat32LE,
    originModuleId: "ml-runtime",
    traceId: traceId);

// Derived tensor (after transformation)
var derived = parent.Derive(
    dtype: TensorDType.Float32,
    shape: [7],
    layout: TensorLayout.Dense,
    shapeClass: TensorShapeClass.ExactStatic,
    data: reshapedData,
    encoding: TensorEncoding.RawFloat32LE,
    lineageStep: TensorLineageRecord.Create(
        parentTensorIds: [parent.Id],
        transformationStepId: "reshape",
        producingModuleId: "transformation-bus",
        kernelVersion: "1.0",
        operations: ["reshape:[1,7]→[7]"]));
```

---

## 6. Module Contract Declaration

Tensor-aware modules must implement `ITensorContract` to declare their accepted input/output contracts:

```csharp
public sealed class MLRuntimeTensorContract : ITensorContract
{
    public string ModuleId => "ml-runtime";
    public TensorCertificationLevel CertificationLevel => TensorCertificationLevel.TensorNative;

    public IReadOnlySet<TensorDType> AcceptedInputDTypes =>
        new HashSet<TensorDType> { TensorDType.Float32 };

    public IReadOnlySet<TensorShapeClass> AcceptedInputShapeClasses =>
        new HashSet<TensorShapeClass> { TensorShapeClass.ExactStatic };

    public IReadOnlySet<TensorLayout> AcceptedInputLayouts =>
        new HashSet<TensorLayout> { TensorLayout.Dense };

    public IReadOnlySet<TensorDType> EmittedOutputDTypes =>
        new HashSet<TensorDType> { TensorDType.Float32 };

    public IReadOnlySet<TensorShapeClass> EmittedOutputShapeClasses =>
        new HashSet<TensorShapeClass> { TensorShapeClass.ExactStatic };

    public bool IsCompatible(BcgTensor tensor) =>
        AcceptedInputDTypes.Contains(tensor.DType) &&
        AcceptedInputShapeClasses.Contains(tensor.ShapeClass) &&
        AcceptedInputLayouts.Contains(tensor.Layout);

    public string? GetIncompatibilityReason(BcgTensor tensor)
    {
        if (!AcceptedInputDTypes.Contains(tensor.DType))
            return $"DType {tensor.DType} not accepted; expected Float32.";
        if (!AcceptedInputShapeClasses.Contains(tensor.ShapeClass))
            return $"ShapeClass {tensor.ShapeClass} not accepted; expected ExactStatic.";
        if (!AcceptedInputLayouts.Contains(tensor.Layout))
            return $"Layout {tensor.Layout} not accepted; expected Dense.";
        return null;
    }
}
```
