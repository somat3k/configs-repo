using Microsoft.Extensions.Logging;

namespace MLS.AIHub.Providers;

/// <summary>
/// Base class providing Circuit Breaker logic shared by all <see cref="ILLMProvider"/> implementations.
/// Marks a provider unavailable for 60 seconds after 3 consecutive failures.
/// </summary>
public abstract class ProviderBase(ILogger logger) : ILLMProvider
{
    private const int FailureThreshold = 3;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AvailabilityProbeTimeout = TimeSpan.FromMilliseconds(500);

    private int _consecutiveFailures;
    // Store as UTC ticks for lock-free atomic update via Interlocked.Exchange
    private long _circuitOpenedAtTicks = DateTimeOffset.MinValue.UtcTicks;

    /// <inheritdoc/>
    public abstract string ProviderId { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<string> SupportedModels { get; }

    /// <inheritdoc/>
    public virtual bool IsAvailable
    {
        get
        {
            if (_consecutiveFailures < FailureThreshold)
                return true;

            // Allow re-check after circuit open duration
            var openedAt = new DateTimeOffset(Interlocked.Read(ref _circuitOpenedAtTicks), TimeSpan.Zero);
            return DateTimeOffset.UtcNow - openedAt >= CircuitOpenDuration;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            logger.LogDebug("Provider {ProviderId} circuit open — skipping probe", ProviderId);
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(AvailabilityProbeTimeout);

        try
        {
            var available = await ProbeAsync(timeoutCts.Token).ConfigureAwait(false);
            if (available)
            {
                Interlocked.Exchange(ref _consecutiveFailures, 0);
            }
            else
            {
                RecordFailure();
            }
            return available;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordFailure();
            logger.LogWarning(ex, "Provider {ProviderId} availability probe failed", ProviderId);
            return false;
        }
    }

    /// <summary>
    /// Provider-specific availability probe implementation.
    /// Called with a 500 ms timeout already applied.
    /// </summary>
    protected abstract Task<bool> ProbeAsync(CancellationToken ct);

    /// <inheritdoc/>
    public abstract Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService BuildService(string modelId);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Records a failure and trips the circuit breaker if the threshold is exceeded.</summary>
    protected void RecordFailure()
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= FailureThreshold)
        {
            Interlocked.Exchange(ref _circuitOpenedAtTicks, DateTimeOffset.UtcNow.UtcTicks);
            logger.LogWarning("Provider {ProviderId} circuit opened after {Failures} consecutive failures",
                ProviderId, failures);
        }
    }
}
