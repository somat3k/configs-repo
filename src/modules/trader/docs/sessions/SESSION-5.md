# trader — Session 5: Tensor Contract Compliance

> Use this document as context when generating Trader module code with GitHub Copilot.

---

## Overview

Session 5 makes `trader` tensor-compatible via the transformation bus. The module declares its `ITensorContract` for `MLSignal` and `FeatureVector` sockets, receives transformed tensors from the bus, and emits `TENSOR_LINEAGE_CREATED` when it derives a new trade decision tensor from an inference output.

---

## Module Tensor Contract

```csharp
// Accepted inputs: Float32, [ExactStatic or BoundedDynamic], Dense
// Emitted outputs: Float32 (trade decision scores), [ExactStatic], Dense
// Certification: CompatibleViaTransformationBus
```

---

## Signal Engine Integration

`SignalEngine` receives inference output tensors from `ml-runtime`:
- Shape: `[1, 3]` (BUY / SELL / HOLD probabilities)
- DType: `Float32`, Layout: `Dense`, ShapeClass: `ExactStatic`
- The engine reads `data` and produces a `TradeSignalPayload` — no raw tensor emitted externally
- Tensor lineage is tracked for audit: `origin_block_id` = `SignalEngine` block ID

---

## Certification Level

`TensorCertificationLevel.CompatibleViaTransformationBus` — test classes 3.1, 3.2, 3.3, 3.5, 3.9 required.

---

## References

- `docs/bcg/universal-tensor-contract.md`
- `docs/bcg/tensor-qa-gates.md`
- `src/core/MLS.Core/Tensor/ITensorContract.cs`
