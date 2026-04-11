namespace MLS.Core.Kernels;

/// <summary>
/// Immutable descriptor used to certify and resolve kernels.
/// </summary>
/// <param name="OperationId">Canonical operation identity used for registry resolution.</param>
/// <param name="InputContract">Declared input contract identifier.</param>
/// <param name="OutputContract">Declared output contract identifier.</param>
/// <param name="StateClass">Declared state behavior class.</param>
/// <param name="ExecutionModes">Supported execution modes.</param>
/// <param name="PerformanceBudget">Declared performance objectives.</param>
/// <param name="Version">Kernel semantic version.</param>
/// <param name="TensorCompatibilityProfile">Tensor compatibility profile identifier.</param>
public sealed record KernelDescriptor(
    string OperationId,
    string InputContract,
    string OutputContract,
    KernelStateClass StateClass,
    IReadOnlyList<KernelExecutionMode> ExecutionModes,
    KernelPerformanceBudget PerformanceBudget,
    string Version,
    string TensorCompatibilityProfile)
{
    /// <summary>
    /// Creates a validated descriptor instance.
    /// </summary>
    public static KernelDescriptor Create(
        string operationId,
        string inputContract,
        string outputContract,
        KernelStateClass stateClass,
        IReadOnlyList<KernelExecutionMode> executionModes,
        KernelPerformanceBudget performanceBudget,
        string version = "1.0.0",
        string tensorCompatibilityProfile = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(tensorCompatibilityProfile);
        ArgumentNullException.ThrowIfNull(executionModes);
        ArgumentNullException.ThrowIfNull(performanceBudget);

        if (executionModes.Count == 0)
        {
            throw new ArgumentException("At least one execution mode is required.", nameof(executionModes));
        }

        return new KernelDescriptor(
            OperationId: operationId,
            InputContract: inputContract,
            OutputContract: outputContract,
            StateClass: stateClass,
            ExecutionModes: executionModes,
            PerformanceBudget: performanceBudget,
            Version: version,
            TensorCompatibilityProfile: tensorCompatibilityProfile);
    }
}
