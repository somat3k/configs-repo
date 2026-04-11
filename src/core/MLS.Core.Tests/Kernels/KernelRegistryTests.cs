using FluentAssertions;
using MLS.Core.Kernels;
using MLS.Core.Tensor;
using Xunit;

namespace MLS.Core.Tests.Kernels;

/// <summary>
/// Unit tests for <see cref="KernelRegistry"/>.
/// </summary>
public sealed class KernelRegistryTests
{
    [Fact]
    public void RegisterAndResolve_ReturnsKernelInstance()
    {
        var registry = new KernelRegistry();
        var descriptor = KernelDescriptor.Create(
            operationId: "test-op",
            inputContract: "tensor/inference-input:v1",
            outputContract: "tensor/inference-output:v1",
            stateClass: KernelStateClass.Pure,
            executionModes: [KernelExecutionMode.Sync],
            performanceBudget: new KernelPerformanceBudget(TimeSpan.FromMilliseconds(10), null, false));

        registry.Register("test-op", new TestKernelFactory());

        var kernel = registry.Resolve(descriptor, new NoopServiceProvider());

        kernel.Should().NotBeNull();
        kernel.Descriptor.OperationId.Should().Be("test-op");
    }

    [Fact]
    public void Resolve_MissingOperation_ThrowsKeyNotFoundException()
    {
        var registry = new KernelRegistry();
        var descriptor = KernelDescriptor.Create(
            operationId: "missing-op",
            inputContract: "input",
            outputContract: "output",
            stateClass: KernelStateClass.Pure,
            executionModes: [KernelExecutionMode.Sync],
            performanceBudget: new KernelPerformanceBudget(TimeSpan.FromMilliseconds(5), null, false));

        var act = () => registry.Resolve(descriptor, new NoopServiceProvider());

        act.Should().Throw<KeyNotFoundException>();
    }

    private sealed class NoopServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TestKernelFactory : IKernelFactory
    {
        public IKernel CreateKernel(KernelDescriptor descriptor, IServiceProvider serviceProvider) =>
            new TestKernel(descriptor);
    }

    private sealed class TestKernel(KernelDescriptor descriptor) : IKernel
    {
        public KernelDescriptor Descriptor { get; } = descriptor;

        public KernelState State { get; private set; } = KernelState.Uninitialized;

        public Task InitAsync(KernelExecutionContext context)
        {
            State = KernelState.Ready;
            return Task.CompletedTask;
        }

        public Task<KernelOutput> ExecuteAsync(BcgTensor input, KernelExecutionContext context)
        {
            var output = new KernelOutput(input, true, 0, context.TraceId);
            return Task.FromResult(output);
        }

        public Task DisposeAsync(CancellationToken cancellationToken = default)
        {
            State = KernelState.Disposing;
            return Task.CompletedTask;
        }
    }
}
