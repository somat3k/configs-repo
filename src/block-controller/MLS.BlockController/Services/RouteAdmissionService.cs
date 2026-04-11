using Microsoft.Extensions.Logging;
using MLS.BlockController.Models;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.BlockController;

namespace MLS.BlockController.Services;

/// <summary>
/// Implements the routing gate sequence from <c>docs/bcg/routing-policy-spec.md</c>.
/// <list type="number">
///   <item>Capability gate — at least one module declares the operation</item>
///   <item>Health gate — at least one capable module is in a routeable health state</item>
///   <item>Score and select — highest composite score wins</item>
/// </list>
/// </summary>
public sealed class RouteAdmissionService(
    ICapabilityRegistry _capabilities,
    IModuleHealthTracker _health,
    IMessageRouter _router,
    ILogger<RouteAdmissionService> _logger) : IRouteAdmissionService
{
    private const string ControllerId = "block-controller";
    private const string RuntimeMode  = "Normal";

    // Routeable health states — only modules in these states may receive new work
    private static readonly IReadOnlySet<ModuleHealthState> _routableStates = new HashSet<ModuleHealthState>
    {
        ModuleHealthState.Healthy,
    };

    /// <inheritdoc/>
    public async Task<RouteAdmissionResult> AdmitAsync(
        string operationType,
        Guid requestId,
        CancellationToken ct = default)
    {
        // Gate 1 — capability
        var candidates = await _capabilities.ResolveByOperationAsync(operationType, ct).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            var reason = RouteRejectionReasons.NoCapableModule;
            _logger.LogWarning("Route rejected ({Reason}): no module handles {Op}", reason, operationType);
            await EmitRejectionAsync(requestId, operationType, reason, evaluated: 0, ct).ConfigureAwait(false);
            return RouteAdmissionResult.Rejected(reason, candidatesEvaluated: 0);
        }

        // Gate 2 — health; score candidates that pass
        int evaluated = 0;
        RouteAdmissionResult? best = null;

        foreach (var record in candidates)
        {
            evaluated++;
            var state = await _health.GetHealthStateAsync(record.ModuleId, ct).ConfigureAwait(false);

            if (state is null || !_routableStates.Contains(state.Value))
            {
                _logger.LogDebug("Candidate {Name} ({Id}) skipped: health={State}",
                    record.ModuleName, record.ModuleId, state?.ToString() ?? "unknown");
                continue;
            }

            var score = ComputeScore(record, state.Value);

            if (best is null || score > best.Score)
                best = RouteAdmissionResult.Admitted(record.ModuleId, score, evaluated);
        }

        if (best is null)
        {
            var reason = RouteRejectionReasons.NoHealthyModule;
            _logger.LogWarning("Route rejected ({Reason}): {Count} capable modules exist but none are healthy",
                reason, evaluated);
            await EmitRejectionAsync(requestId, operationType, reason, evaluated, ct).ConfigureAwait(false);
            return RouteAdmissionResult.Rejected(reason, evaluated);
        }

        _logger.LogDebug("Route admitted: op={Op} → module={Id} score={Score}",
            operationType, best.TargetModuleId, best.Score);

        return best with { CandidatesEvaluated = evaluated };
    }

    // ── Score computation ────────────────────────────────────────────────────────

    private static int ComputeScore(Models.CapabilityRecord record, ModuleHealthState state)
    {
        // CapabilityMatchScore: specialist modules score higher (fewer declared ops = better match)
        int capScore = Math.Max(0, 100 - record.OperationTypes.Count);

        // HealthScore per routing-policy-spec.md
        int healthScore = state switch
        {
            ModuleHealthState.Healthy   => 50,
            ModuleHealthState.Degraded  => 30,  // not in _routableStates but kept for future fallback lane
            _                           => 0,
        };

        return capScore + healthScore;
    }

    // ── Event emission ───────────────────────────────────────────────────────────

    private async Task EmitRejectionAsync(
        Guid requestId, string workloadType, string reason, int evaluated, CancellationToken ct)
    {
        var payload = new RouteRejectedPayload(
            RequestId: requestId,
            WorkloadType: workloadType,
            Reason: reason,
            CandidatesEvaluated: evaluated,
            CandidatesAdmitted: 0,
            RuntimeMode: RuntimeMode,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(MessageTypes.RouteRejected, ControllerId, payload);
        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);
    }
}

/// <summary>Structured rejection reason codes for <see cref="IRouteAdmissionService"/>.</summary>
public static class RouteRejectionReasons
{
    /// <summary>No module in the registry declares the requested operation type.</summary>
    public const string NoCapableModule          = "ROUTE_REJECTED_NO_CAPABLE_MODULE";

    /// <summary>Capable modules exist but none are in a routeable health state.</summary>
    public const string NoHealthyModule          = "ROUTE_REJECTED_NO_HEALTHY_MODULE";

    /// <summary>The current runtime mode prohibits the workload class.</summary>
    public const string PolicyDenied             = "ROUTE_REJECTED_POLICY_DENIED";

    /// <summary>No compatible transport path exists to any capable module.</summary>
    public const string TransportIncompatible    = "ROUTE_REJECTED_TRANSPORT_INCOMPATIBLE";

    /// <summary>All retry budget consumed; final attempt failed.</summary>
    public const string RetryExhausted           = "ROUTE_REJECTED_RETRY_EXHAUSTED";

    /// <summary>No safe path exists after all fallback options are exhausted.</summary>
    public const string NoSafePath               = "ROUTE_REJECTED_NO_SAFE_PATH";
}
