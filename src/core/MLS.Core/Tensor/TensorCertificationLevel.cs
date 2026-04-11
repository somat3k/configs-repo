namespace MLS.Core.Tensor;

/// <summary>
/// QA certification outcome for a module's tensor compliance.
/// A module must pass all applicable test classes before entering a production tensor lane.
/// </summary>
public enum TensorCertificationLevel
{
    /// <summary>
    /// Module natively produces and consumes BCG tensors.
    /// Passes all applicable test classes including lineage, storage, and transport.
    /// </summary>
    TensorNative,

    /// <summary>
    /// Module accepts and emits tensors only through the platform transformation bus.
    /// Does not handle raw tensor I/O directly.
    /// </summary>
    CompatibleViaTransformationBus,

    /// <summary>
    /// Module receives and observes tensors but never produces them.
    /// Must pass tensor contract validation and storage routing tests.
    /// </summary>
    ReadOnlyConsumer,

    /// <summary>
    /// Module produces tensors as a specialist output (e.g. DataEvolution, TensorTrainer).
    /// Must pass full production, lineage, and serialisation test classes.
    /// </summary>
    ProducingSpecialist,

    /// <summary>
    /// Module has not passed the required test classes.
    /// Must not be used in production tensor lanes until certified.
    /// </summary>
    NotCertified,
}
