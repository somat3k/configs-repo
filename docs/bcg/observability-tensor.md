# Tensor Observability Requirements
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `src/core/MLS.Core/Contracts/Tensor/`
> **Status**: ✅ Frozen for Phase 0

---

## 1. Purpose

Every tensor-critical action must produce enough telemetry to reconstruct the full execution history of any tensor. The observability system must support:

- **Provenance reconstruction**: where did this tensor originate?
- **Validation audit**: who validated it and what was the outcome?
- **Transformation tracing**: who transformed it and what operations were applied?
- **Rejection analysis**: who rejected it and why?
- **Latency attribution**: how long did the tensor spend in queues, transport, and computation?
- **Storage tracking**: where was the payload cached or externalized?
- **Consumption attribution**: which module and route consumed this tensor?

---

## 2. Mandatory Telemetry Fields

Every tensor-critical envelope payload must carry the following telemetry fields:

| Field | Type | Description |
|-------|------|-------------|
| `tensor_id` | `Guid` | Identity of the tensor being observed |
| `trace_id` | `Guid` | Session/request correlation — stable across the lineage |
| `origin_block_id` | `Guid?` | Block that produced the tensor (if applicable) |
| `producer_module_id` | `string` | Module that created or last transformed the tensor |
| `consumer_module_id` | `string` | Module that received or rejected the tensor |
| `dtype` | `TensorDType` | Declared dtype at the time of the event |
| `shape_summary` | `string` | Human-readable shape (e.g. `"[1, 7]"`) |
| `storage_mode` | `TensorStorageMode` | Current storage tier |
| `transport_class` | `TensorTransportClass` | How the payload was delivered |
| `validation_outcome` | `string` | `"passed"` or failure reason |
| `transformation_outcome` | `string?` | Applied operations or `null` if not transformed |
| `routing_latency_ms` | `double` | Time from tensor receipt to dispatch in the governor |

---

## 3. Observability Events

### 3.1 `TENSOR_ACCEPTED`
Emitted by the Block Controller when a tensor passes validation and enters the routing fabric.

**Payload fields**: `tensor_id`, `trace_id`, `producer_module_id`, `dtype`, `shape_summary`, `contract_version`

### 3.2 `TENSOR_VALIDATION_FAILED`
Emitted by the Block Controller when a tensor fails any validation check.

**Payload**: `TensorValidationFailedPayload`
```csharp
record TensorValidationFailedPayload(
    Guid TensorId,
    Guid TraceId,
    string ProducerModuleId,
    IReadOnlyList<string> FailureReasons,
    int ContractVersion);
```

### 3.3 `TENSOR_ROUTED`
Emitted after a tensor has been dispatched to its target module.

**Payload**: `TensorRoutedPayload`
```csharp
record TensorRoutedPayload(
    Guid TensorId, Guid TraceId,
    string ProducerModuleId, string ConsumerModuleId,
    TensorTransportClass TransportClass,
    TensorStorageMode StorageMode,
    TensorDType DType, string ShapeSummary,
    double RoutingLatencyMs);
```

### 3.4 `TENSOR_TRANSFORMED`
Emitted by the transformation bus after a material transformation is applied.

**Payload**: `TensorTransformedPayload`
```csharp
record TensorTransformedPayload(
    Guid SourceTensorId, Guid DerivedTensorId, Guid TraceId,
    string TransformationStepId, string ProducerModuleId,
    IReadOnlyList<string> Operations, bool IsLossyCast, Guid LineageId);
```

### 3.5 `TENSOR_COMPATIBILITY_ERROR`
Emitted when a tensor cannot be routed to a target module and no legal transformation exists.

**Payload**: `TensorCompatibilityErrorPayload`
```csharp
record TensorCompatibilityErrorPayload(
    Guid TensorId, Guid TraceId,
    string ProducerModuleId, string TargetModuleId,
    TensorDType TensorDType, TensorShapeClass TensorShapeClass, TensorLayout TensorLayout,
    string IncompatibilityReason, bool TransformationAttempted);
```

### 3.6 `TENSOR_STORED`
Emitted when a tensor payload is written to a storage tier.

**Payload**: `TensorStoredPayload`
```csharp
record TensorStoredPayload(
    Guid TensorId, Guid TraceId,
    TensorStorageMode StorageMode, string StorageKey,
    DateTimeOffset? ExpiresAt, string PayloadHash);
```

### 3.7 `TENSOR_LINEAGE_CREATED`
Emitted when a new lineage record is persisted.

**Payload**: `TensorLineageCreatedPayload`
```csharp
record TensorLineageCreatedPayload(
    Guid LineageId, Guid DerivedTensorId,
    IReadOnlyList<Guid> ParentTensorIds, Guid TraceId,
    string ProducingModuleId, string TransformationStepId,
    bool IsLossyCast);
```

---

## 4. Latency Budget Categories

| Phase | Target | Notes |
|-------|--------|-------|
| Validation | < 1 ms | Fingerprint check only for large payloads |
| Compatibility decision | < 2 ms | `ITensorContract.IsCompatible` must be O(1) |
| Route + dispatch | < 5 ms | Must not turn the controller into a bottleneck |
| Transformation bus | Budgeted per operation | Reported in `TENSOR_TRANSFORMED` |
| Storage write (Redis) | < 5 ms | Non-blocking; write queued |
| Storage write (IPFS) | Async | Never on hot path; background task |

---

## 5. Reconstruction Query Specification

An audit system must be able to reconstruct the complete history of a tensor from only the `tensor_id`:

1. Fetch the tensor registry record from Postgres
2. Fetch all `TensorLineageRecord` entries with matching `derived_tensor_id` or in `parent_tensor_ids`
3. Walk the `parent_tensor_ids` recursively until root tensors are reached
4. Replay telemetry events by `trace_id` to reconstruct the execution timeline
5. Verify integrity at any point using `integrity.payload_hash` against the stored payload
