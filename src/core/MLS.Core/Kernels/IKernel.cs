using MLS.Core.Tensor;

namespace MLS.Core.Kernels;

/// <summary>
/// Base contract for all production kernels.
/// </summary>
public interface IKernel
{
    /// <summary>Kernel certification and contract descriptor.</summary>
    KernelDescriptor Descriptor { get; }

    /// <summary>Current lifecycle state for observability.</summary>
    KernelState State { get; }

    /// <summary>Initializes kernel resources for execution.</summary>
    Task InitAsync(KernelExecutionContext context);

    /// <summary>Executes one kernel invocation and returns typed output.</summary>
    Task<KernelOutput> ExecuteAsync(BcgTensor input, KernelExecutionContext context);

    /// <summary>Disposes kernel resources.</summary>
    Task DisposeAsync(CancellationToken cancellationToken = default);
}
