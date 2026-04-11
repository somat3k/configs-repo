using System.Collections.Concurrent;

namespace MLS.Core.Kernels;

/// <summary>
/// Thread-safe kernel factory registry keyed by operation identity.
/// </summary>
public sealed class KernelRegistry
{
    private readonly ConcurrentDictionary<string, IKernelFactory> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers or replaces a kernel factory for an operation identity.
    /// </summary>
    public void Register(string operationId, IKernelFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(factory);

        _factories[operationId] = factory;
    }

    /// <summary>
    /// Resolves a kernel instance from descriptor and service provider.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no factory is registered for the operation.</exception>
    public IKernel Resolve(KernelDescriptor descriptor, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (!_factories.TryGetValue(descriptor.OperationId, out var factory))
        {
            throw new KeyNotFoundException(
                $"No kernel factory is registered for operation '{descriptor.OperationId}'.");
        }

        return factory.CreateKernel(descriptor, serviceProvider);
    }
}
