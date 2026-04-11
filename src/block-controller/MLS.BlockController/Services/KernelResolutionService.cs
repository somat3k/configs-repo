using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.BlockController;
using MLS.Core.Kernels;

namespace MLS.BlockController.Services;

/// <summary>
/// Resolves executable blocks to certified kernel instances and emits lifecycle events.
/// </summary>
public sealed class KernelResolutionService(
    KernelRegistry _registry,
    KernelScheduler _scheduler,
    IMessageRouter _router,
    ILogger<KernelResolutionService> _logger)
{
    private const string ControllerId = "block-controller";

    /// <summary>
    /// Resolves, places, and initializes a kernel for execution.
    /// </summary>
    public async Task<ResolvedKernel> ResolveAsync(
        BlockKernelExecutionRequest request,
        IServiceProvider serviceProvider,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var placement = await _scheduler.PlaceAsync(request.OperationType, request.Descriptor, ct).ConfigureAwait(false);

        var kernel = _registry.Resolve(request.Descriptor, serviceProvider);
        await kernel.InitAsync(request.Context).ConfigureAwait(false);

        await EmitLifecycleAsync(
            MessageTypes.KernelInitialized,
            request,
            placement,
            state: kernel.State.ToString(),
            reason: null,
            ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Kernel resolved for block {BlockId}: op={Operation} module={ModuleId} lane={Lane}",
            request.BlockId,
            request.OperationType,
            placement.TargetModuleId,
            placement.Lane);

        return new ResolvedKernel(kernel, placement.TargetModuleId, placement.Lane);
    }

    /// <summary>
    /// Emits a fault lifecycle event for a kernel execution path.
    /// </summary>
    public Task ReportFaultedAsync(
        BlockKernelExecutionRequest request,
        Guid moduleId,
        KernelExecutionMode lane,
        string reason,
        CancellationToken ct = default) =>
        EmitLifecycleAsync(
            MessageTypes.KernelFaulted,
            request,
            new KernelPlacementDecision(moduleId, lane, score: 0),
            state: KernelState.Faulted.ToString(),
            reason,
            ct);

    /// <summary>
    /// Disposes a resolved kernel and emits disposal telemetry.
    /// </summary>
    public async Task DisposeAsync(
        ResolvedKernel resolved,
        BlockKernelExecutionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(request);

        await resolved.Kernel.DisposeAsync(ct).ConfigureAwait(false);

        await EmitLifecycleAsync(
            MessageTypes.KernelDisposed,
            request,
            new KernelPlacementDecision(resolved.ModuleId, resolved.Lane, score: 0),
            state: KernelState.Disposing.ToString(),
            reason: null,
            ct).ConfigureAwait(false);
    }

    private async Task EmitLifecycleAsync(
        string messageType,
        BlockKernelExecutionRequest request,
        KernelPlacementDecision placement,
        string state,
        string? reason,
        CancellationToken ct)
    {
        var payload = new KernelLifecycleEventPayload(
            BlockId: request.BlockId,
            KernelOperationId: request.Descriptor.OperationId,
            TraceId: request.Context.TraceId,
            ModuleId: placement.TargetModuleId.ToString(),
            LaneId: placement.Lane.ToString(),
            State: state,
            Reason: reason,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(messageType, ControllerId, payload);
        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Block execution request required for kernel resolution.
/// </summary>
/// <param name="BlockId">Logical block instance identifier.</param>
/// <param name="OperationType">Operation identity requested by the block.</param>
/// <param name="Descriptor">Kernel descriptor used for registry resolution.</param>
/// <param name="Context">Execution context for initialization and invocation.</param>
public sealed record BlockKernelExecutionRequest(
    Guid BlockId,
    string OperationType,
    KernelDescriptor Descriptor,
    KernelExecutionContext Context);

/// <summary>
/// Resolved kernel and its assigned placement metadata.
/// </summary>
/// <param name="Kernel">Resolved kernel instance.</param>
/// <param name="ModuleId">Assigned execution module ID.</param>
/// <param name="Lane">Assigned execution lane.</param>
public sealed record ResolvedKernel(IKernel Kernel, Guid ModuleId, KernelExecutionMode Lane);
