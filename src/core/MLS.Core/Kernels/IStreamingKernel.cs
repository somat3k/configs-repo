using MLS.Core.Tensor;

namespace MLS.Core.Kernels;

/// <summary>
/// Contract for kernels that emit progressive outputs.
/// </summary>
public interface IStreamingKernel : IKernel
{
    /// <summary>Streams typed output fragments for one invocation.</summary>
    IAsyncEnumerable<KernelOutput> StreamAsync(BcgTensor input, KernelExecutionContext context);
}
