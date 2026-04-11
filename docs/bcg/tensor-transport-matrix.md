# Tensor Transport Matrix
## BCG Platform — Session 03 Governance Document

> **Reference**: `universal-tensor-contract.md` · `src/core/MLS.Core/Tensor/TensorTransportClass.cs`
> **Status**: ✅ Frozen for Phase 0

---

## Overview

The transport class governs how a tensor payload moves between its producer and consumer. The Block Controller routing governor selects the transport class based on payload size, latency requirements, persistence policy, and lane suitability.

The selection is data-driven, not hard-coded. Producers declare their preferred class; the governor may override based on the size threshold and lane health.

---

## Transport Classes

### Inline Transport

**Use when:**
- Payload is small (below the configurable inline size threshold)
- Low-latency synchronous path is required
- No externalization threshold is exceeded
- Target module is on the same network fabric

**Typical transports:**
- gRPC unary request/response
- SignalR/WebSocket event payload (JSON or MessagePack body)
- HTTP API body for management-scale payloads

**Rules:**
- `transport_class = TensorTransportClass.Inline`
- `data` field is populated in the `BcgTensor`
- `persistence` may be null or point to a Redis cache copy
- Inline tensors must still carry `integrity.payload_hash` for validation

### Stream Transport

**Use when:**
- Partial outputs are meaningful before the full result is available
- A long-running transform produces incremental outputs
- Large series of tensors emerge progressively over time (e.g. training epoch progress)

**Typical transports:**
- gRPC server-streaming
- SignalR/WebSocket streaming (multiple `TENSOR_ROUTED` events on the same trace ID)
- SSE for limited-compatibility flows

**Rules:**
- `transport_class = TensorTransportClass.Stream`
- Each partial tensor in the stream is a first-class `BcgTensor` with its own `id`
- All tensors in the stream share the same `meta.trace_id`
- The final tensor in the stream may carry a `TENSOR_BATCH_COMPLETE` companion envelope

### Reference Transport

**Use when:**
- Payload is large (exceeds the inline size threshold)
- Multiple consumers may need the same object (avoids duplication)
- Replay or archival is required
- Inline transfer would create pressure on the WebSocket fabric

**Typical pattern:**
1. Producer writes payload to the appropriate storage tier (Redis/Postgres/IPFS)
2. Producer creates a `BcgTensor` with `transport_class = Reference` and `data = null`
3. `persistence` field carries the storage reference (`RedisKey`, `PostgresRecordId`, or `IpfsCid`)
4. The envelope with the reference tensor is dispatched inline (metadata only)
5. Consumers fetch the payload on demand (lazy fetch pattern)

**Rules:**
- `transport_class = TensorTransportClass.Reference`
- `data` must be null
- `persistence` must be non-null with a valid storage reference
- `integrity.payload_hash` must be set so consumers can verify the fetched payload

---

## Transport Decision Matrix

| Payload Size | Access Pattern | Latency Need | Selected Class |
|-------------|---------------|-------------|---------------|
| Small (< threshold) | Single consumer | Low | `Inline` |
| Small (< threshold) | Multiple consumers | Low | `Inline` + Redis cache copy |
| Large (≥ threshold) | Single consumer | Normal | `Reference` → IPFS or Postgres |
| Large (≥ threshold) | Multiple consumers | Normal | `Reference` → IPFS |
| Streaming output | Progressive | Low | `Stream` |
| Training artifact | Archival | Batch | `Reference` → IPFS |

---

## Size Threshold Policy

The inline vs. reference decision is governed by a configurable byte-size threshold. The threshold must be:

- Measurable (byte count of the serialised `data` payload)
- Environment-aware (may differ between development, staging, and production)
- Applied consistently by the Block Controller governor

**Principle**: large payloads must not destabilize the fabric, regardless of the threshold value.

The threshold configuration key is `Tensor:InlineSizeThresholdBytes` in the Block Controller environment configuration.

---

## Transport and Envelope Integration

Transport class selection integrates with the existing `EnvelopePayload` protocol:

```csharp
// Inline: tensor data is embedded in the envelope payload
var envelope = EnvelopePayload.Create(
    type: MessageTypes.TensorRouted,
    moduleId: _moduleId,
    payload: new TensorRoutedPayload(
        TensorId: tensor.Id,
        TraceId: tensor.Meta.TraceId,
        ProducerModuleId: tensor.Meta.OriginModuleId,
        ConsumerModuleId: targetModuleId,
        TransportClass: TensorTransportClass.Inline,
        StorageMode: TensorStorageMode.Transient,
        DType: tensor.DType,
        ShapeSummary: ShapeToString(tensor.Shape),
        RoutingLatencyMs: latency));
```
