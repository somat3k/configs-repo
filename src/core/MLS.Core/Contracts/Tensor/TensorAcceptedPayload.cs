using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_ACCEPTED</c>.
/// Emitted by the Block Controller when a tensor passes all validation checks
/// and enters the routing fabric. This event confirms successful contract validation
/// before the routing decision is made.
/// </summary>
/// <param name="TensorId">ID of the accepted tensor.</param>
/// <param name="TraceId">Trace correlation ID.</param>
/// <param name="ProducerModuleId">Module that produced the tensor.</param>
/// <param name="DType">Declared dtype of the tensor.</param>
/// <param name="ShapeSummary">Human-readable shape string (e.g. <c>"[1, 7]"</c>).</param>
/// <param name="ContractVersion">Contract version declared on the tensor metadata.</param>
/// <param name="Layout">Physical layout of the tensor.</param>
/// <param name="TransportClass">Transport class of the tensor at the time of acceptance.</param>
public sealed record TensorAcceptedPayload(
    [property: JsonPropertyName("tensor_id")] Guid TensorId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("producer_module_id")] string ProducerModuleId,
    [property: JsonPropertyName("dtype")] TensorDType DType,
    [property: JsonPropertyName("shape_summary")] string ShapeSummary,
    [property: JsonPropertyName("contract_version")] int ContractVersion,
    [property: JsonPropertyName("layout")] TensorLayout Layout,
    [property: JsonPropertyName("transport_class")] TensorTransportClass TransportClass);
