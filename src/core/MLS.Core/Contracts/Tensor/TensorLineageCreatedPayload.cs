using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_LINEAGE_CREATED</c>.
/// Emitted when a new lineage record is persisted, allowing audit systems and replay
/// engines to maintain an up-to-date causal ancestry chain.
/// </summary>
/// <param name="LineageId">ID of the new lineage record.</param>
/// <param name="DerivedTensorId">ID of the tensor this lineage step produced.</param>
/// <param name="ParentTensorIds">IDs of the parent tensors referenced by this step.</param>
/// <param name="TraceId">Trace correlation ID.</param>
/// <param name="ProducingModuleId">Module that created the lineage record.</param>
/// <param name="TransformationStepId">Identifier of the transformation step.</param>
/// <param name="IsLossyCast">Whether a lossy cast occurred in this step.</param>
public sealed record TensorLineageCreatedPayload(
    [property: JsonPropertyName("lineage_id")] Guid LineageId,
    [property: JsonPropertyName("derived_tensor_id")] Guid DerivedTensorId,
    [property: JsonPropertyName("parent_tensor_ids")] IReadOnlyList<Guid> ParentTensorIds,
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("producing_module_id")] string ProducingModuleId,
    [property: JsonPropertyName("transformation_step_id")] string TransformationStepId,
    [property: JsonPropertyName("is_lossy_cast")] bool IsLossyCast);
