# Module Capability Registry Specification
## BCG Session 02 — Capability Declaration and Registry Contract

> **Status**: ✅ Active  
> **Last Updated**: Session 02  
> **Owned by**: Block Controller (block-controller)

## 1. Purpose

This document defines the format, validation rules, and lifecycle of module capability declarations in the Block Controller's capability registry. Every module must declare capabilities at registration time. Capabilities are the machine-readable contract that enables governed routing.

## 2. Capability Declaration Format

Capability declarations are submitted as part of the `POST /api/modules/register` request via the `capabilities` field (currently a `string[]`). In Session 02 this field is extended to support richer structured values through a tiered approach:

- **Tier 1 (current)**: simple string tags, e.g. `["trading", "ml-inference", "heartbeat"]`
- **Tier 2 (this session)**: structured capability records with operation, tensor, and transport dimensions
- **Tier 3 (later sessions)**: signed capability certificates with version and compatibility vectors

### 2.1 Structured Capability Record (Tier 2)

```json
{
  "operation_types": ["INFERENCE_REQUEST", "TRAINING_JOB_START"],
  "tensor_classes_in": ["Float32Dense", "Float64Dense"],
  "tensor_classes_out": ["Float32Dense"],
  "transport_interfaces": ["websocket", "http"],
  "batch_support": "parallel",
  "streaming_support": "output-only",
  "stateful": false,
  "version": "1.0.0",
  "compatibility_min": "0.9.0"
}
```

### 2.2 Required Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `operation_types` | `string[]` | ✅ | Envelope `Type` values this module handles |
| `transport_interfaces` | `string[]` | ✅ | `["websocket"]`, `["http"]`, or both |
| `version` | `string` | ✅ | Semantic version of the capability declaration |
| `tensor_classes_in` | `string[]` | ❌ | Required for ML/tensor-capable modules |
| `tensor_classes_out` | `string[]` | ❌ | Required for ML/tensor-capable modules |
| `batch_support` | `string` | ❌ | `none`, `sequential`, `parallel`, `pipeline` |
| `streaming_support` | `string` | ❌ | `none`, `input-only`, `output-only`, `bidirectional` |
| `stateful` | `bool` | ❌ | Whether the module maintains state across requests |

## 3. Validation Rules

The controller validates capability declarations at registration time:

1. `operation_types` must contain at least one value
2. `transport_interfaces` must contain at least one value
3. `version` must be a valid semantic version string
4. Unknown or misspelled operation types produce a warning (not a rejection) in Session 02; hard rejection in Session 04
5. A module registering with `tensor_classes_in` but no `tensor_classes_out` is valid (consumer-only)

## 4. Registry Data Model

The `ICapabilityRegistry` stores one `CapabilityRecord` per registered module:

```csharp
public sealed record CapabilityRecord(
    Guid ModuleId,
    string ModuleName,
    IReadOnlyList<string> OperationTypes,
    IReadOnlyList<string> TensorClassesIn,
    IReadOnlyList<string> TensorClassesOut,
    IReadOnlyList<string> TransportInterfaces,
    string BatchSupport,
    string StreamingSupport,
    bool IsStateful,
    string Version,
    DateTimeOffset RegisteredAt,
    DateTimeOffset LastUpdatedAt);
```

## 5. Registry Operations

| Operation | Method | Notes |
|-----------|--------|-------|
| Register capability | `RegisterAsync(Guid, CapabilityRecord)` | Overwrites on duplicate moduleId |
| Resolve by operation | `ResolveByOperationAsync(string operationType)` | Returns all capable module IDs |
| Get record | `GetAsync(Guid moduleId)` | Returns null if not registered |
| Update | `UpdateAsync(Guid, CapabilityRecord)` | Emits `MODULE_CAPABILITY_UPDATED` |
| Evict | `EvictAsync(Guid moduleId)` | Called on heartbeat timeout or deregister |

## 6. Scoring Integration

`ResolveByOperationAsync` returns candidates ordered by pre-computed `CapabilityMatchScore` (see `routing-policy-spec.md`). The Route Governor uses this list as input for health scoring and final selection.

## 7. Capability Update Lifecycle

```
Module registers → Capability stored → Route Governor queries registry
     ↓
Heartbeat times out → Capability evicted → Routes to module rejected
     ↓
Module re-registers → Capability restored → Routes available again
```

Any update to a module's capability record emits `MODULE_CAPABILITY_UPDATED` as a broadcast envelope so all subscribers can invalidate cached route decisions.

## 8. Future Extensions (Session 04+)

- Capability declarations will carry a cryptographic signature issued by the controller after validation
- Compatibility vectors will enable automatic transform-chain planning
- The registry will be persisted to PostgreSQL for restart continuity

## 9. References

- `block-controller-authority-model.md` — authority model
- `routing-policy-spec.md` — score computation
- `health-escalation-model.md` — health state affects capability routing
- `src/block-controller/MLS.BlockController/Services/ICapabilityRegistry.cs` — C# interface
- `src/block-controller/MLS.BlockController/Services/InMemoryCapabilityRegistry.cs` — implementation
