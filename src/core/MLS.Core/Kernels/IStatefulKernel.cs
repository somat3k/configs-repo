namespace MLS.Core.Kernels;

/// <summary>
/// Contract for kernels with bounded persisted state.
/// </summary>
public interface IStatefulKernel : IKernel
{
    /// <summary>Captures a checkpoint for controlled recovery.</summary>
    Task<KernelCheckpointState> SnapshotAsync(KernelExecutionContext context);

    /// <summary>Restores a previously captured checkpoint.</summary>
    Task RestoreAsync(KernelCheckpointState checkpoint, KernelExecutionContext context);

    /// <summary>Resets mutable kernel state to a clean baseline.</summary>
    Task ResetAsync(KernelExecutionContext context);
}
