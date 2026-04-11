# ml-runtime — Session 5: Tensor Contract Compliance

> Use this document as context when generating ML Runtime module code with GitHub Copilot.

---

## Overview

Session 5 makes `ml-runtime` tensor-native. The module implements `ITensorContract`, validates incoming `BcgTensor` inputs against its declared dtype/shape/layout policy, produces output tensors via `BcgTensor.Derive`, emits lineage records, and routes large artifacts via reference transport.

---

## Module Tensor Contract

```csharp
// Accepted inputs: Float32, [ExactStatic], Dense
// Emitted outputs: Float32, [ExactStatic], Dense
// Certification: TensorNative
```

**Input shape contract for `model-t`**: `[1, 7]` — 7 feature vector (ExactStatic)
**Output shape contract for `model-t`**: `[1, 3]` — per-class probabilities (ExactStatic)

---

## Hot-Reload Integration

When `TRAINING_JOB_COMPLETE` arrives:
1. Load new ONNX model from IPFS CID in `TrainingJobCompletePayload`
2. Emit `TENSOR_LINEAGE_CREATED` with the model artifact as the new tensor
3. Swap the model in `ModelRegistry` (swap-first, 500ms delayed dispose)

---

## Certification Level

`TensorCertificationLevel.TensorNative` — all 9 QA gate test classes required.

---

## References

- `docs/bcg/universal-tensor-contract.md`
- `docs/bcg/tensor-qa-gates.md`
- `docs/bcg/shape-dtype-governance.md`
- `src/core/MLS.Core/Tensor/ITensorContract.cs`
- `src/core/MLS.Core/Tensor/BcgTensor.cs`
