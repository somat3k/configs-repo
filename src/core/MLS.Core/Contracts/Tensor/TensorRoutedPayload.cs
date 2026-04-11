using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_ROUTED</c>.
/// Emitted by the Block Controller after a tensor has been successfully validated and
/// dispatched to its target module. Used for observability and latency tracking.
/// </summary>
/// <param name="TensorId">ID of the routed tensor.</param>
/// <param name="TraceId">Trace correlation ID.</param>
/// <param name="ProducerModuleId">Module that produced the tensor.</param>
/// <param name="ConsumerModuleId">Module that received the tensor.</param>
/// <param name="TransportClass">Transport class used for delivery.</param>
/// <param name="StorageMode">Storage mode at the time of routing.</param>
/// <param name="DType">Declared dtype.</param>
/// <param name="ShapeSummary">Human-readable shape string (e.g. <c>"[1, 7]"</c>).</param>
/// <param name="RoutingLatencyMs">Time in milliseconds from tensor receipt to dispatch.</param>
public sealed record TensorRoutedPayload(
    [property: JsonPropertyName("tensor_id")] Guid TensorId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("producer_module_id")] string ProducerModuleId,
    [property: JsonPropertyName("consumer_module_id")] string ConsumerModuleId,
    [property: JsonPropertyName("transport_class")] TensorTransportClass TransportClass,
    [property: JsonPropertyName("storage_mode")] TensorStorageMode StorageMode,
    [property: JsonPropertyName("dtype")] TensorDType DType,
    [property: JsonPropertyName("shape_summary")] string ShapeSummary,
    [property: JsonPropertyName("routing_latency_ms")] double RoutingLatencyMs);
