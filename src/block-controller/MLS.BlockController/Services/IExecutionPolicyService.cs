namespace MLS.BlockController.Services;

/// <summary>
/// Execution lane classification per <c>docs/bcg/routing-policy-spec.md</c>.
/// </summary>
public enum ExecutionLane
{
    /// <summary>Immediate sync control: &lt; 10 ms budget, no retries.</summary>
    A,

    /// <summary>Standard synchronous: &lt; 50 ms budget, 1 retry.</summary>
    B,

    /// <summary>Batch execution: throughput-optimised, scheduler-driven.</summary>
    C,

    /// <summary>Streaming execution: long-lived, partial result emission.</summary>
    D,

    /// <summary>Deferred async job: training / large transforms, no latency SLO.</summary>
    E,
}

/// <summary>
/// The policy applied to an admitted execution request.
/// </summary>
/// <param name="Lane">Assigned execution lane.</param>
/// <param name="TimeoutMs">Timeout in milliseconds (null = no SLO enforced, e.g. Lane E).</param>
/// <param name="MaxRetries">Maximum retry attempts (0 = no retries).</param>
public sealed record ExecutionPolicy(ExecutionLane Lane, int? TimeoutMs, int MaxRetries);

/// <summary>
/// Evaluates and assigns execution policy (lane, timeout, retry budget) for admitted route requests.
/// See <c>docs/bcg/routing-policy-spec.md</c> section 5.
/// </summary>
public interface IExecutionPolicyService
{
    /// <summary>
    /// Return the execution policy for the given operation type.
    /// Called after route admission to assign lane and enforcement parameters.
    /// </summary>
    ExecutionPolicy GetPolicy(string operationType);

    /// <summary>
    /// Determine whether the request should be admitted in the current runtime mode.
    /// Returns a rejection reason string when denied; <see langword="null"/> when admitted.
    /// </summary>
    string? EvaluateRuntimeMode(string operationType, string runtimeMode);
}
