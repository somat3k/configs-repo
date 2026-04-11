using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.BlockController.Models;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Kernels;
using MLS.Core.Tensor;
using Moq;
using Xunit;

namespace MLS.Core.Tests.Kernels;

/// <summary>
/// Integration test for block-to-kernel resolution flow.
/// </summary>
public sealed class KernelResolutionIntegrationTests
{
    private static readonly JsonElement TensorData = JsonDocument.Parse("[1.0,2.0,3.0,4.0,5.0,6.0,7.0]").RootElement;

    [Fact]
    public async Task PureKernel_ResolvesAndExecutes_EndToEnd()
    {
        var router = new Mock<IMessageRouter>();
        router.Setup(r => r.BroadcastAsync(It.IsAny<EnvelopePayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var capabilityRegistry = new InMemoryCapabilityRegistry(
            router.Object,
            NullLogger<InMemoryCapabilityRegistry>.Instance);

        var healthTracker = new ModuleHealthTracker(
            router.Object,
            NullLogger<ModuleHealthTracker>.Instance);

        var scheduler = new KernelScheduler(
            capabilityRegistry,
            healthTracker,
            NullLogger<KernelScheduler>.Instance);

        var kernelRegistry = new KernelRegistry();
        kernelRegistry.Register("PURE_KERNEL", new PureKernelFactory());

        var resolver = new KernelResolutionService(
            kernelRegistry,
            scheduler,
            router.Object,
            NullLogger<KernelResolutionService>.Instance);

        var moduleId = Guid.NewGuid();
        await capabilityRegistry.RegisterAsync(new CapabilityRecord(
            ModuleId: moduleId,
            ModuleName: "kernel-test-module",
            OperationTypes: ["PURE_KERNEL"],
            TensorClassesIn: [],
            TensorClassesOut: [],
            TransportInterfaces: ["websocket"],
            BatchSupport: "none",
            StreamingSupport: "none",
            IsStateful: false,
            Version: "1.0.0",
            RegisteredAt: DateTimeOffset.UtcNow,
            LastUpdatedAt: DateTimeOffset.UtcNow));

        await healthTracker.InitializeAsync(moduleId, "kernel-test-module");
        await healthTracker.RecordHeartbeatAsync(moduleId);

        var descriptor = KernelDescriptor.Create(
            operationId: "PURE_KERNEL",
            inputContract: "tensor/inference-input:v1",
            outputContract: "tensor/inference-output:v1",
            stateClass: KernelStateClass.Pure,
            executionModes: [KernelExecutionMode.Sync],
            performanceBudget: new KernelPerformanceBudget(TimeSpan.FromMilliseconds(5), null, false));

        var context = KernelExecutionContext.Create();
        var request = new BlockKernelExecutionRequest(Guid.NewGuid(), "PURE_KERNEL", descriptor, context);

        var resolved = await resolver.ResolveAsync(request, new NoopServiceProvider());

        resolved.ModuleId.Should().Be(moduleId);
        resolved.Lane.Should().Be(KernelExecutionMode.Sync);

        var input = BcgTensor.CreateRoot(
            dtype: TensorDType.Float32,
            shape: [1, 7],
            layout: TensorLayout.Dense,
            shapeClass: TensorShapeClass.ExactStatic,
            data: TensorData,
            encoding: TensorEncoding.RawFloat32LE,
            originModuleId: "kernel-test-module",
            traceId: context.TraceId);

        var output = await resolved.Kernel.ExecuteAsync(input, context);
        output.IsFinal.Should().BeTrue();
        output.TraceId.Should().Be(context.TraceId);

        await resolver.DisposeAsync(resolved, request);

        router.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.KernelInitialized),
                It.IsAny<CancellationToken>()),
            Times.Once);

        router.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.KernelDisposed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class NoopServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class PureKernelFactory : IKernelFactory
    {
        public IKernel CreateKernel(KernelDescriptor descriptor, IServiceProvider serviceProvider) =>
            new PureKernel(descriptor);
    }

    private sealed class PureKernel(KernelDescriptor descriptor) : IKernel
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
            State = KernelState.Running;
            return Task.FromResult(new KernelOutput(input, true, 0, context.TraceId));
        }

        public Task DisposeAsync(CancellationToken cancellationToken = default)
        {
            State = KernelState.Disposing;
            return Task.CompletedTask;
        }
    }
}
