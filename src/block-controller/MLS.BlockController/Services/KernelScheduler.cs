using MLS.BlockController.Models;
using MLS.Core.Kernels;

namespace MLS.BlockController.Services;

/// <summary>
/// Policy-aware scheduler that selects a target module and execution lane for kernel work.
/// </summary>
public sealed class KernelScheduler(
    ICapabilityRegistry _capabilities,
    IModuleHealthTracker _health,
    ILogger<KernelScheduler> _logger)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, double> _loadFactors = new();

    private static readonly IReadOnlySet<ModuleHealthState> RouteableStates = new HashSet<ModuleHealthState>
    {
        ModuleHealthState.Healthy,
        ModuleHealthState.Degraded,
    };

    /// <summary>
    /// Upserts an observed load factor for scheduler scoring.
    /// </summary>
    public void UpdateLoadFactor(Guid moduleId, double loadFactor)
    {
        _loadFactors[moduleId] = Math.Clamp(loadFactor, 0d, 1d);
    }

    /// <summary>
    /// Selects the highest-scoring module for a kernel operation.
    /// </summary>
    public async Task<KernelPlacementDecision> PlaceAsync(
        string operationType,
        KernelDescriptor descriptor,
        CancellationToken ct = default)
    {
        var candidates = await _capabilities.ResolveByOperationAsync(operationType, ct).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No capable module found for operation '{operationType}'.");
        }

        CapabilityRecord? best = null;
        var bestScore = int.MinValue;

        foreach (var candidate in candidates)
        {
            var state = await _health.GetHealthStateAsync(candidate.ModuleId, ct).ConfigureAwait(false);
            if (state is null || !RouteableStates.Contains(state.Value))
            {
                continue;
            }

            var score = ComputeScore(candidate, state.Value);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException($"No routeable module found for operation '{operationType}'.");
        }

        var lane = ResolveLane(descriptor);
        _logger.LogDebug("Kernel placement selected module={ModuleId} lane={Lane} score={Score}",
            best.ModuleId, lane, bestScore);

        return new KernelPlacementDecision(best.ModuleId, lane, bestScore);
    }

    private int ComputeScore(CapabilityRecord record, ModuleHealthState state)
    {
        var capabilityScore = Math.Max(0, 100 - record.OperationTypes.Count);
        var healthScore = state == ModuleHealthState.Healthy ? 50 : 30;
        var loadFactor = _loadFactors.TryGetValue(record.ModuleId, out var load) ? load : 0d;
        var loadScore = (int)Math.Round((1d - loadFactor) * 25d);

        return capabilityScore + healthScore + loadScore;
    }

    private static KernelExecutionMode ResolveLane(KernelDescriptor descriptor)
    {
        if (descriptor.ExecutionModes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Kernel descriptor '{descriptor.OperationId}' does not declare any execution mode.");
        }

        if (descriptor.ExecutionModes.Contains(KernelExecutionMode.Streaming))
        {
            return KernelExecutionMode.Streaming;
        }

        if (descriptor.ExecutionModes.Contains(KernelExecutionMode.HeavyCompute))
        {
            return KernelExecutionMode.HeavyCompute;
        }

        return descriptor.ExecutionModes[0];
    }
}

/// <summary>
/// Result of scheduler placement.
/// </summary>
/// <param name="TargetModuleId">Selected module identifier.</param>
/// <param name="Lane">Assigned execution lane.</param>
/// <param name="Score">Composite scheduling score.</param>
public sealed record KernelPlacementDecision(Guid TargetModuleId, KernelExecutionMode Lane, int Score);
