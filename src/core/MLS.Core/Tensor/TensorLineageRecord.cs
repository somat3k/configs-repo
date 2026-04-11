using System.Text.Json.Serialization;

namespace MLS.Core.Tensor;

/// <summary>
/// A single step in the immutable lineage chain of a <see cref="BcgTensor"/>.
/// Every material transformation that produces a new tensor identity must append
/// a <see cref="TensorLineageRecord"/> referencing the parent tensor(s).
/// </summary>
/// <param name="LineageId">Unique identifier for this lineage record.</param>
/// <param name="ParentTensorIds">
/// One or more tensor IDs that contributed to the new tensor.
/// Must be non-empty; every tensor except root tensors must reference at least one parent.
/// </param>
/// <param name="TransformationStepId">
/// Stable identifier for the computation or transformation step that produced the new tensor
/// (e.g. a transformation bus operation name, kernel version, or block ID).
/// </param>
/// <param name="ProducingModuleId">Registered module ID of the service that performed the transformation.</param>
/// <param name="ProducingBlockId">Block ID within the module if applicable, or <see langword="null"/>.</param>
/// <param name="KernelVersion">
/// Version of the kernel or transformation function that produced the new tensor.
/// Enables exact replay of the transformation step.
/// </param>
/// <param name="Timestamp">UTC time at which the transformation was performed.</param>
/// <param name="Operations">
/// Ordered list of atomic operations applied during this transformation step
/// (e.g. <c>"cast:float64→float32"</c>, <c>"reshape:[1,7]→[7]"</c>, <c>"pad:0→10"</c>).
/// </param>
/// <param name="IsLossyCast">
/// <see langword="true"/> when a cast in this step is lossy.
/// Lossy casts require explicit lineage marking and policy approval.
/// </param>
/// <param name="PersistenceRelocationNote">
/// Human-readable note when the payload was relocated between storage tiers in this step.
/// </param>
/// <param name="CompatibilityNotes">
/// Free-text notes about dtype or shape compatibility decisions made during this step.
/// </param>
public sealed record TensorLineageRecord(
    [property: JsonPropertyName("lineage_id")] Guid LineageId,
    [property: JsonPropertyName("parent_tensor_ids")] IReadOnlyList<Guid> ParentTensorIds,
    [property: JsonPropertyName("transformation_step_id")] string TransformationStepId,
    [property: JsonPropertyName("producing_module_id")] string ProducingModuleId,
    [property: JsonPropertyName("producing_block_id")] Guid? ProducingBlockId,
    [property: JsonPropertyName("kernel_version")] string KernelVersion,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("operations")] IReadOnlyList<string> Operations,
    [property: JsonPropertyName("is_lossy_cast")] bool IsLossyCast,
    [property: JsonPropertyName("persistence_relocation_note")] string? PersistenceRelocationNote,
    [property: JsonPropertyName("compatibility_notes")] string? CompatibilityNotes)
{
    /// <summary>Creates a new lineage record with a generated ID and the current UTC time.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parentTensorIds"/> or <paramref name="operations"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="parentTensorIds"/> is empty or contains empty GUIDs,
    /// or when any of <paramref name="transformationStepId"/>, <paramref name="producingModuleId"/>,
    /// or <paramref name="kernelVersion"/> is null or whitespace,
    /// or when any element of <paramref name="operations"/> is null or whitespace.
    /// </exception>
    public static TensorLineageRecord Create(
        IReadOnlyList<Guid> parentTensorIds,
        string transformationStepId,
        string producingModuleId,
        string kernelVersion,
        IReadOnlyList<string> operations,
        Guid? producingBlockId = null,
        bool isLossyCast = false,
        string? persistenceRelocationNote = null,
        string? compatibilityNotes = null)
    {
        ArgumentNullException.ThrowIfNull(parentTensorIds);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(transformationStepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(producingModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelVersion);

        if (parentTensorIds.Count == 0)
            throw new ArgumentException("At least one parent tensor ID is required.", nameof(parentTensorIds));

        var parentTensorIdsCopy = new Guid[parentTensorIds.Count];
        for (var i = 0; i < parentTensorIds.Count; i++)
        {
            if (parentTensorIds[i] == Guid.Empty)
                throw new ArgumentException("Parent tensor IDs must not contain empty GUID values.", nameof(parentTensorIds));
            parentTensorIdsCopy[i] = parentTensorIds[i];
        }

        var operationsCopy = new string[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operations[i], nameof(operations));
            operationsCopy[i] = operations[i];
        }

        return new TensorLineageRecord(
            LineageId: Guid.NewGuid(),
            ParentTensorIds: parentTensorIdsCopy,
            TransformationStepId: transformationStepId,
            ProducingModuleId: producingModuleId,
            ProducingBlockId: producingBlockId,
            KernelVersion: kernelVersion,
            Timestamp: DateTimeOffset.UtcNow,
            Operations: operationsCopy,
            IsLossyCast: isLossyCast,
            PersistenceRelocationNote: persistenceRelocationNote,
            CompatibilityNotes: compatibilityNotes);
    }
}
