# arbitrager — Session 5: Tensor Contract Compliance

> Use this document as context when generating Arbitrager module code with GitHub Copilot.

---

## Overview

Session 5 makes `arbitrager` tensor-compatible via the transformation bus. The module declares its `ITensorContract` for the `OpportunityScorer` inputs (nHOP path feature vectors), receives transformed tensors from the bus, and uses them in ONNX model-a inference.

---

## Module Tensor Contract

```csharp
// Accepted inputs: Float32, [ExactStatic], Dense
// Input shape for model-a: [1, N] where N is the path feature count
// Emitted outputs: Float32 (opportunity score), [ExactStatic], Dense
// Certification: CompatibleViaTransformationBus
```

---

## Scorer Integration

`OpportunityScorer` receives a path feature tensor:
- Built by `ArrayBuilder` with slippage 0.5%
- Converted to `BcgTensor` with `TensorDType.Float32`, `TensorLayout.Dense`
- Scored by ONNX model-a; output carries lineage back to the path scan

---

## Certification Level

`TensorCertificationLevel.CompatibleViaTransformationBus` — test classes 3.1, 3.2, 3.3, 3.5, 3.9 required.

---

## References

- `docs/bcg/universal-tensor-contract.md`
- `docs/bcg/tensor-qa-gates.md`
- `src/core/MLS.Core/Tensor/ITensorContract.cs`
