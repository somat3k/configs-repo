using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_TRANSFORMED</c>.
/// Emitted by the transformation bus when a material transformation has been applied,
/// producing a new tensor identity with lineage back to the source.
/// </summary>
/// <param name="SourceTensorId">ID of the original tensor that was transformed.</param>
/// <param name="DerivedTensorId">ID of the new tensor that was produced.</param>
/// <param name="TraceId">Trace correlation ID (unchanged across the transformation).</param>
/// <param name="TransformationStepId">Identifier of the transformation step applied.</param>
/// <param name="ProducerModuleId">Module that performed the transformation.</param>
/// <param name="Operations">Ordered list of operations performed (e.g. <c>"cast:float64→float32"</c>).</param>
/// <param name="IsLossyCast">Whether any applied cast was lossy.</param>
/// <param name="LineageId">ID of the <see cref="TensorLineageRecord"/> created for this step.</param>
public sealed record TensorTransformedPayload(
    [property: JsonPropertyName("source_tensor_id")] Guid SourceTensorId,
    [property: JsonPropertyName("derived_tensor_id")] Guid DerivedTensorId,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("transformation_step_id")] string TransformationStepId,
    [property: JsonPropertyName("producer_module_id")] string ProducerModuleId,
    [property: JsonPropertyName("operations")] IReadOnlyList<string> Operations,
    [property: JsonPropertyName("is_lossy_cast")] bool IsLossyCast,
    [property: JsonPropertyName("lineage_id")] Guid LineageId);
