# Tensor Lifecycle Model
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `src/core/MLS.Core/Tensor/BcgTensor.cs`
> **Status**: ✅ Frozen for Phase 0

---

## Overview

Every BCG tensor passes through a defined lifecycle from birth to expiry or archival. The lifecycle governs where the tensor may be in-flight, who is responsible for it, and what actions are legal at each stage.

---

## Stage 1 — Creation

A tensor is created by exactly one of:

| Producer | Example |
|----------|---------|
| Block kernel | RSIBlock emits a float32 feature vector |
| Transport adapter | External price feed normalized into a sequence tensor |
| DataEvolution transform | Arbitrary source normalized into a canonical tensor |
| TensorTrainer output | Evaluation tensor from a completed training pass |
| ML Runtime inference result | Model output tensor from ONNX inference |
| External ingestion path | Batch archive loaded from IPFS |

**Creation rules**:
- `BcgTensor.CreateRoot(...)` must be called; no ad hoc construction
- `meta.origin_module_id` is mandatory
- `meta.contract_version` must equal `TensorMeta.CurrentContractVersion`
- `meta.trace_id` must be supplied by the calling context (not generated inside `CreateRoot`)
- `shape` must be fully declared; `-1` is allowed only for axes the module contract explicitly marks as dynamic

---

## Stage 2 — Validation

Before entering the routing fabric, every tensor is validated by the Block Controller governor:

| Check | Rule |
|-------|------|
| Contract version | Must equal current contract version |
| DType legality | Must be a named `TensorDType` member |
| Shape legality | No `null` shape; rank must be within module lane limits |
| Payload integrity | Hash check when `integrity.payload_hash` is present |
| Origin metadata completeness | `meta.origin_module_id` must be non-empty |

**Failure outcome**: the Block Controller emits `TENSOR_VALIDATION_FAILED` back to the producer and drops the tensor. No silent acceptance of invalid tensors.

---

## Stage 3 — Routing Decision

The Block Controller selects a transport class and target module based on:

| Factor | Determines |
|--------|-----------|
| Target module's `ITensorContract` | Whether the tensor is natively compatible |
| Tensor size vs. threshold | Inline vs. Reference transport |
| Lane health | Whether the target module is available |
| Shape class vs. lane suitability | Whether a static-batch lane accepts the tensor |
| Presence of a transformation path | Whether the transformation bus must be invoked |

**Routing outcomes**:
- `TENSOR_ROUTED` emitted on successful dispatch
- `TENSOR_COMPATIBILITY_ERROR` emitted when no legal route exists
- Transformation bus invoked when dtype or shape requires adaptation

---

## Stage 4 — Execution Use

Inside the consuming module, the tensor may be:

| Action | Notes |
|--------|-------|
| Consumed directly | ONNX inference, indicator computation |
| Transformed | Cast, reshape, slice → creates new tensor with lineage |
| Batched | Collected with other tensors for batch inference |
| Streamed | Partial outputs delivered incrementally |
| Externalized | Payload too large; reference written to Redis/Postgres/IPFS |
| Cached | Hot tensor cached in Redis for downstream consumers |

---

## Stage 5 — Persistence / Expiration

| Tier | When Used | TTL Policy |
|------|-----------|-----------|
| `Transient` | Local working state only; never published | Garbage-collected |
| `Redis` | Hot tensors, recent inference outputs, stream windows | Short TTL (configurable per lane) |
| `Postgres` | Registry metadata, lineage records, audit events | Permanent until archived |
| `IPFS` | Large payloads, training artifacts, dataset snapshots | Content-addressed; permanent |

**Threshold principle**: A configurable byte-size threshold determines when tensors stop traveling inline and begin traveling by reference. The threshold is environment-aware but the principle is fixed: large payloads must not destabilize the fabric.

---

## Stage 6 — Lineage Continuation

If the tensor is materially transformed at any stage, the transformation produces a new `BcgTensor` with:
- A new `id`
- `Lineage` list extended with the new `TensorLineageRecord`
- `meta.trace_id` inherited from the parent
- `TENSOR_LINEAGE_CREATED` emitted so audit systems can update the ancestry chain

The original tensor remains immutable. Both tensors exist concurrently until the parent expires or is archived.

---

## Lifecycle State Diagram

```
[External / Block / Adapter]
         │
         │  CreateRoot(...)
         ▼
    ┌─────────────┐
    │   CREATED   │
    └──────┬──────┘
           │  Submit to routing governor
           ▼
    ┌─────────────┐       ┌────────────────────────┐
    │  VALIDATING │──────►│  TENSOR_VALIDATION_     │
    └──────┬──────┘  fail │  FAILED (dropped)       │
           │ pass         └────────────────────────┘
           ▼
    ┌─────────────┐       ┌────────────────────────┐
    │   ROUTING   │──────►│  TENSOR_COMPATIBILITY_  │
    └──────┬──────┘  fail │  ERROR (route failed)   │
           │ success      └────────────────────────┘
           ▼
    ┌─────────────┐
    │   IN USE    │◄─────────────────────────────────┐
    └──────┬──────┘                                  │
           │                                         │ TENSOR_TRANSFORMED
           ├──► cached (Redis)                       │ (new tensor created)
           ├──► persisted (Postgres / IPFS)          │
           ├──► transformed ─────────────────────────┘
           └──► expired / archived
```
