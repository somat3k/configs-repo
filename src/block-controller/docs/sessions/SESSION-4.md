# block-controller — Session 4: Tensor Governance

> Use this document as context when generating Block Controller code with GitHub Copilot.

---

## Overview

Session 4 installs tensor governance into the Block Controller. The Block Controller becomes the **routing governor** for BCG tensors: it validates incoming tensors, resolves module contracts, selects transport classes, invokes the transformation bus on incompatible tensors, and emits structured observability events for every tensor lifecycle transition.

---

## Governor Responsibilities

```
                  Incoming BcgTensor envelope
                           │
                           ▼
            ┌──────────────────────────┐
            │  TensorValidationService │  ← validates contract version, dtype,
            └──────────┬───────────────┘    shape, payload hash, origin metadata
                       │ pass / fail
                       ▼
            ┌──────────────────────────┐
            │   ModuleContractResolver │  ← resolves ITensorContract for target
            └──────────┬───────────────┘
                       │ compatible / needs transform / incompatible
                       ▼
            ┌──────────────────────────┐
            │  TransformationBusClient │  ← invoked only when ITensorContract
            └──────────┬───────────────┘    reports incompatibility with legal path
                       │ derived BcgTensor
                       ▼
            ┌──────────────────────────┐
            │  TensorRoutingDispatcher │  ← selects Inline / Stream / Reference
            └──────────────────────────┘    emits TENSOR_ROUTED
```

---

## New Message Types (Session 4)

All constants are defined in `MessageTypes.Tensor.cs`:

| Constant | Direction | Description |
|----------|-----------|-------------|
| `TENSOR_VALIDATION_FAILED` | BC → producer | Tensor rejected at validation |
| `TENSOR_ROUTED` | BC → telemetry | Tensor dispatched to consumer |
| `TENSOR_COMPATIBILITY_ERROR` | BC → producer | No legal route or transformation exists |
| `TENSOR_ACCEPTED` | BC → telemetry | Tensor passed validation and entered fabric |
| `TENSOR_TRANSFORMED` | Bus → BC | Material transformation applied |
| `TENSOR_STORED` | BC → telemetry | Payload written to storage tier |
| `TENSOR_LINEAGE_CREATED` | Any → BC | New lineage record persisted |
| `TENSOR_BATCH_COMPLETE` | Runtime → BC | Batch inference or training pass finished |

---

## Inline Size Threshold

Configuration key: `Tensor:InlineSizeThresholdBytes`

| Environment | Default |
|-------------|---------|
| Development | 65536 (64 KB) |
| Production | 1048576 (1 MB) |

---

## Skills Applied

- `.skills/dotnet-devs.md` — primary constructors, C# records, channel fan-out
- `.skills/websockets-inferences.md` — SignalR envelope routing
- `.skills/storage-data-management.md` — Redis / Postgres / IPFS routing
- `.skills/beast-development.md` — zero-alloc hot path, `ArrayPool`, fingerprint checks
- `.skills/machine-learning.md` — tensor shape discipline, ONNX alignment

---

## References

- `docs/bcg/session-03-extended-document.md`
- `docs/bcg/universal-tensor-contract.md`
- `docs/bcg/tensor-lifecycle.md`
- `docs/bcg/tensor-compatibility-transformation.md`
- `docs/bcg/observability-tensor.md`
- `src/core/MLS.Core/Tensor/BcgTensor.cs`
- `src/core/MLS.Core/Tensor/ITensorContract.cs`
- `src/core/MLS.Core/Constants/MessageTypes.Tensor.cs`
