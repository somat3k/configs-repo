namespace MLS.Core.Kernels;

/// <summary>
/// Factory for producing kernel instances from certified descriptors.
/// </summary>
public interface IKernelFactory
{
    /// <summary>Create a kernel instance for the supplied descriptor.</summary>
    IKernel CreateKernel(KernelDescriptor descriptor, IServiceProvider serviceProvider);
}
