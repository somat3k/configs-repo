using MLS.Core.Constants;

namespace MLS.BlockController.Services;

/// <summary>
/// Default implementation of <see cref="IExecutionPolicyService"/>.
/// Lane and timeout assignments follow <c>docs/bcg/routing-policy-spec.md</c> section 5.
/// </summary>
public sealed class ExecutionPolicyService : IExecutionPolicyService
{
    // ── Lane assignments ─────────────────────────────────────────────────────────

    private static readonly Dictionary<string, ExecutionPolicy> _policies = new(StringComparer.OrdinalIgnoreCase)
    {
        // Lane A — immediate control-plane (< 10 ms, no retries)
        [MessageTypes.ModuleRegister]   = new(ExecutionLane.A, TimeoutMs: 10,   MaxRetries: 0),
        [MessageTypes.ModuleHeartbeat]  = new(ExecutionLane.A, TimeoutMs: 10,   MaxRetries: 0),
        [MessageTypes.ModuleDeregister] = new(ExecutionLane.A, TimeoutMs: 10,   MaxRetries: 0),

        // Lane B — standard synchronous (< 50 ms, 1 retry)
        [MessageTypes.TradeSignal]          = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.InferenceRequest]     = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.InferenceResult]      = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.ArbitrageOpportunity] = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.OrderCreate]          = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.OrderCancel]          = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.OrderConfirmation]    = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),
        [MessageTypes.OrderRejection]       = new(ExecutionLane.B, TimeoutMs: 50,   MaxRetries: 1),

        // Lane D — streaming (long-lived, no fixed timeout)
        [MessageTypes.ShellOutput]          = new(ExecutionLane.D, TimeoutMs: null, MaxRetries: 0),
        [MessageTypes.ShellInput]           = new(ExecutionLane.D, TimeoutMs: null, MaxRetries: 0),

        // Lane E — deferred async (no latency SLO)
        [MessageTypes.ShellExecRequest]     = new(ExecutionLane.E, TimeoutMs: null, MaxRetries: 0),
    };

    private static readonly ExecutionPolicy _defaultPolicy = new(ExecutionLane.B, TimeoutMs: 50, MaxRetries: 1);

    // ── Runtime mode admission rules ─────────────────────────────────────────────

    /// <summary>Operation types blocked in Maintenance mode.</summary>
    private static readonly HashSet<string> _blockedInMaintenance = new(StringComparer.OrdinalIgnoreCase)
    {
        MessageTypes.InferenceRequest,
        MessageTypes.TradeSignal,
        MessageTypes.ArbitrageOpportunity,
        MessageTypes.OrderCreate,
    };

    /// <inheritdoc/>
    public ExecutionPolicy GetPolicy(string operationType) =>
        _policies.TryGetValue(operationType, out var policy) ? policy : _defaultPolicy;

    /// <inheritdoc/>
    public string? EvaluateRuntimeMode(string operationType, string runtimeMode) =>
        runtimeMode.Equals("Maintenance", StringComparison.OrdinalIgnoreCase)
        && _blockedInMaintenance.Contains(operationType)
            ? RouteRejectionReasons.PolicyDenied
            : null;
}
