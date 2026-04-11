using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_COMPATIBILITY_ERROR</c>.
/// Emitted by the Block Controller when a tensor cannot be legally transformed
/// to match the target module's declared contract. No fallback silent reinterpretation
/// is permitted — the route fails explicitly with this payload.
/// </summary>
/// <param name="TensorId">ID of the incompatible tensor.</param>
/// <param name="TraceId">Trace correlation ID.</param>
/// <param name="ProducerModuleId">Module that produced the tensor.</param>
/// <param name="TargetModuleId">Module that could not accept the tensor.</param>
/// <param name="TensorDType">Declared dtype of the tensor.</param>
/// <param name="TensorShapeClass">Declared shape class of the tensor.</param>
/// <param name="TensorLayout">Declared layout of the tensor.</param>
/// <param name="IncompatibilityReason">Human-readable explanation of the compatibility failure.</param>
/// <param name="TransformationAttempted">Whether the transformation bus was invoked before this error.</param>
public sealed record TensorCompatibilityErrorPayload(
    [property: JsonPropertyName("tensor_id")] Guid TensorId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("producer_module_id")] string ProducerModuleId,
    [property: JsonPropertyName("target_module_id")] string TargetModuleId,
    [property: JsonPropertyName("tensor_dtype")] TensorDType TensorDType,
    [property: JsonPropertyName("tensor_shape_class")] TensorShapeClass TensorShapeClass,
    [property: JsonPropertyName("tensor_layout")] TensorLayout TensorLayout,
    [property: JsonPropertyName("incompatibility_reason")] string IncompatibilityReason,
    [property: JsonPropertyName("transformation_attempted")] bool TransformationAttempted);
