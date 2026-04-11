namespace MLS.Core.Kernels;

/// <summary>
/// Runtime execution context passed into kernel lifecycle and execution calls.
/// </summary>
/// <param name="TraceId">Trace identifier for end-to-end observability.</param>
/// <param name="CorrelationId">Correlation identifier for operation grouping.</param>
/// <param name="CancellationToken">Cancellation token for cooperative cancellation.</param>
/// <param name="Timeout">Optional execution timeout budget.</param>
/// <param name="TenantId">Optional tenant identifier.</param>
/// <param name="ResourceBudget">Optional resource-budget classification string.</param>
public sealed record KernelExecutionContext(
    Guid TraceId,
    Guid CorrelationId,
    CancellationToken CancellationToken,
    TimeSpan? Timeout,
    string? TenantId,
    string? ResourceBudget)
{
    /// <summary>
    /// Creates a context with generated trace/correlation identifiers.
    /// </summary>
    public static KernelExecutionContext Create(
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        string? tenantId = null,
        string? resourceBudget = null) =>
        new(
            TraceId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            CancellationToken: cancellationToken,
            Timeout: timeout,
            TenantId: tenantId,
            ResourceBudget: resourceBudget);
}
