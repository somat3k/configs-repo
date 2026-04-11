using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_VALIDATION_FAILED</c>.
/// Emitted by the Block Controller when an inbound tensor fails the universal contract checks.
/// </summary>
/// <param name="TensorId">ID of the rejected tensor.</param>
/// <param name="TraceId">Trace correlation ID from the rejected tensor's metadata.</param>
/// <param name="ProducerModuleId">Module that produced the invalid tensor.</param>
/// <param name="FailureReasons">Ordered list of specific validation failures.</param>
/// <param name="ContractVersion">Contract version declared on the tensor.</param>
public sealed record TensorValidationFailedPayload(
    [property: JsonPropertyName("tensor_id")] Guid TensorId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("producer_module_id")] string ProducerModuleId,
    [property: JsonPropertyName("failure_reasons")] IReadOnlyList<string> FailureReasons,
    [property: JsonPropertyName("contract_version")] int ContractVersion);
