# Tensor QA Gates
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `src/core/MLS.Core/Tensor/TensorCertificationLevel.cs`
> **Status**: ✅ Frozen for Phase 0

---

## 1. Overview

A module is not tensor-certified unless it passes all test classes applicable to its declared certification level. Certification is a prerequisite for participation in any production tensor lane.

---

## 2. Certification Levels

| Level | C# Enum Value | Meaning |
|-------|--------------|---------|
| **Tensor Native** | `TensorNative` | Module natively produces and consumes BCG tensors — full compliance |
| **Compatible via Bus** | `CompatibleViaTransformationBus` | Module works with tensors only through the transformation bus |
| **Read-Only Consumer** | `ReadOnlyConsumer` | Module receives and observes tensors but never produces them |
| **Producing Specialist** | `ProducingSpecialist` | Module produces tensors as a specialist output (DataEvolution, TensorTrainer) |
| **Not Certified** | `NotCertified` | Module has not passed required tests — must not enter production tensor lanes |

---

## 3. Required Test Classes

### 3.1 Tensor Contract Validation Tests
- Verify that the module's `ITensorContract.AcceptedInputDTypes` rejects tensors with incompatible dtypes
- Verify that `ITensorContract.IsCompatible` returns `false` with an explicit reason for each incompatible combination
- Verify that `ITensorContract.GetIncompatibilityReason` returns non-null for every combination that `IsCompatible` rejects

### 3.2 DType Compatibility Tests
- Test all accepted dtypes from `AcceptedInputDTypes` are handled without error
- Test that dtypes outside the accepted set trigger a `TENSOR_COMPATIBILITY_ERROR` (not a runtime exception)
- If the module performs casts, test that lossy casts set `is_lossy_cast = true` in the lineage record

### 3.3 Shape Compatibility Tests
- Test each `TensorShapeClass` in `AcceptedInputShapeClasses` is handled correctly
- Test that `ExactStatic` shapes are validated precisely (wrong rank or wrong dimension rejects)
- Test that `BoundedDynamic` shapes within declared bounds are accepted
- Test that shapes exceeding declared bounds are rejected with a typed error

### 3.4 Transformation Correctness Tests
- Verify that all transformation operations produce a new tensor ID via `BcgTensor.Derive`
- Verify that `TENSOR_LINEAGE_CREATED` is emitted after every transformation
- Verify that operations are recorded in `TensorLineageRecord.operations` in the correct notation
- Verify that the derived tensor's `meta.trace_id` equals the parent's

### 3.5 Transport Serialisation Tests
- Verify that `BcgTensor` round-trips through JSON serialisation without data loss
- Verify that `TensorEncoding.RawFloat32LE` payloads survive serialisation with correct byte order
- Verify that reference tensors (`TensorTransportClass.Reference`) have `data == null` and a valid `persistence` reference

### 3.6 Storage Routing Tests
- Verify that tensors below the threshold are routed as `Inline`
- Verify that tensors at or above the threshold are routed as `Reference`
- Verify that `TENSOR_STORED` is emitted after a successful storage write
- Verify that `TensorPersistenceRef.IsExternalized` returns `true` for Redis, Postgres, and IPFS modes

### 3.7 Lineage Persistence Tests
- Verify that `TensorLineageRecord.Create` produces unique IDs
- Verify that multi-parent lineage correctly references all parent tensor IDs
- Verify that `TENSOR_LINEAGE_CREATED` envelope is emitted before the derived tensor is dispatched to consumers
- Verify that lineage records survive a module restart (persisted in Postgres)

### 3.8 Replay and Recovery Tests
- Verify that the full ancestry chain of a tensor can be reconstructed from only its `id`
- Verify that a root tensor has `IsRoot == true` and empty `Lineage`
- Verify that a derived tensor at depth N has `Lineage.Count == N`

### 3.9 Performance Tests for Hot Tensor Paths
- `ITensorContract.IsCompatible` must complete in < 0.5 ms for any single call
- `BcgTensor.CreateRoot` must complete in < 0.1 ms for small dense tensors
- `BcgTensor.Derive` must complete in < 0.5 ms for single-step derivation
- Validation of a small tensor must complete in < 1 ms

---

## 4. Certification Matrix by Module

| Module | Expected Level | Required Test Classes |
|--------|---------------|----------------------|
| `ml-runtime` | `TensorNative` | All 9 classes |
| `trader` | `CompatibleViaTransformationBus` | 3.1, 3.2, 3.3, 3.5, 3.9 |
| `arbitrager` | `CompatibleViaTransformationBus` | 3.1, 3.2, 3.3, 3.5, 3.9 |
| `block-controller` | `TensorNative` (governor) | All 9 classes |
| `DataEvolution` (future) | `ProducingSpecialist` | All 9 classes |
| `TensorTrainer` (future) | `ProducingSpecialist` | All 9 classes |
| `web-app` | `ReadOnlyConsumer` | 3.1, 3.5 |
| `data-layer` | `CompatibleViaTransformationBus` | 3.1, 3.2, 3.5, 3.6, 3.7 |

---

## 5. Certification Gate Checklist

Before a module is declared tensor-certified:

- [ ] `ITensorContract` implementation registered and unit-tested
- [ ] All required test classes passing at 100%
- [ ] `TENSOR_VALIDATION_FAILED` and `TENSOR_COMPATIBILITY_ERROR` events observable in test runs
- [ ] `TENSOR_LINEAGE_CREATED` emitted for all transformation paths
- [ ] Performance benchmarks within budget for hot-path operations
- [ ] Module SESSION doc updated with certification level and test count
- [ ] PR reviewed against this document and `universal-tensor-contract.md`
