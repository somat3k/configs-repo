using System.Collections.Concurrent;
using MLS.DataLayer.Persistence;

namespace MLS.DataLayer.Hydra;

/// <summary>
/// Manages the lifecycle of all active exchange feed collection jobs.
/// Supports starting, stopping, and querying the status of individual
/// <see cref="FeedCollector"/> tasks keyed by <see cref="FeedKey"/>.
/// </summary>
public sealed class FeedScheduler(
    IServiceScopeFactory _scopeFactory,
    HyperliquidFeedCollector _hyperliquid,
    CamelotFeedCollector _camelot,
    ILogger<FeedScheduler> _logger) : IAsyncDisposable
{
    private sealed record FeedJob(Task Task, CancellationTokenSource Cts);

    private readonly ConcurrentDictionary<FeedKey, FeedJob> _jobs = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a feed collection job for the given <paramref name="key"/> if one is not
    /// already running.  The job runs until explicitly stopped or the scheduler is disposed.
    /// </summary>
    /// <returns><see langword="true"/> if a new job was started; <see langword="false"/> if
    /// a job for this key was already active.</returns>
    public bool StartFeed(FeedKey key)
    {
        var safeExchange  = HydraUtils.SanitiseFeedId(key.Exchange);
        var safeSymbol    = HydraUtils.SanitiseFeedId(key.Symbol);
        var safeTimeframe = HydraUtils.SanitiseFeedId(key.Timeframe);

        if (_jobs.ContainsKey(key))
        {
            _logger.LogDebug("Feed [{Exchange}/{Symbol}/{Timeframe}] already active — skipping start",
                safeExchange, safeSymbol, safeTimeframe);
            return false;
        }

        var cts       = new CancellationTokenSource();
        var collector = ResolveCollector(key.Exchange);

        if (collector is null)
        {
            _logger.LogWarning("No collector registered for exchange '{Exchange}'", safeExchange);
            cts.Dispose();
            return false;
        }

        var task = Task.Run(() => RunFeedAsync(collector, key, cts.Token), cts.Token);

        if (_jobs.TryAdd(key, new FeedJob(task, cts)))
        {
            _logger.LogInformation("Feed [{Exchange}/{Symbol}/{Timeframe}] started",
                safeExchange, safeSymbol, safeTimeframe);
            return true;
        }

        // Another thread won the race
        cts.Cancel();
        cts.Dispose();
        return false;
    }

    /// <summary>
    /// Stops the feed collection job for the given <paramref name="key"/> and waits
    /// for the task to complete.
    /// </summary>
    public async Task StopFeedAsync(FeedKey key)
    {
        if (!_jobs.TryRemove(key, out var job)) return;

        await job.Cts.CancelAsync().ConfigureAwait(false);

        try { await job.Task.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* expected */ }
        finally { job.Cts.Dispose(); }

        _logger.LogInformation("Feed [{Exchange}/{Symbol}/{Timeframe}] stopped",
            HydraUtils.SanitiseFeedId(key.Exchange),
            HydraUtils.SanitiseFeedId(key.Symbol),
            HydraUtils.SanitiseFeedId(key.Timeframe));
    }

    /// <summary>Returns a snapshot of all currently active feed keys.</summary>
    public IReadOnlyList<FeedKey> ActiveFeeds() => [.. _jobs.Keys];

    /// <summary>
    /// Returns the status of a specific feed job:
    /// <c>Running</c>, <c>Faulted</c>, <c>Completed</c>, or <c>NotFound</c>.
    /// </summary>
    public string GetStatus(FeedKey key)
    {
        if (!_jobs.TryGetValue(key, out var job)) return "NotFound";
        return job.Task.Status switch
        {
            TaskStatus.Running            => "Running",
            TaskStatus.Faulted            => "Faulted",
            TaskStatus.RanToCompletion    => "Completed",
            TaskStatus.Canceled           => "Canceled",
            _                             => job.Task.Status.ToString()
        };
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        var keys = _jobs.Keys.ToArray();
        foreach (var key in keys)
            await StopFeedAsync(key).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RunFeedAsync(FeedCollector collector, FeedKey key, CancellationToken ct)
    {
        // Each feed job owns its own DI scope so it gets a fresh DbContext
        await using var scope      = _scopeFactory.CreateAsyncScope();
        var repository             = scope.ServiceProvider.GetRequiredService<CandleRepository>();
        await collector.RunAsync(key, repository, ct).ConfigureAwait(false);
    }

    private FeedCollector? ResolveCollector(string exchange) => exchange.ToLowerInvariant() switch
    {
        "hyperliquid" => _hyperliquid,
        "camelot"     => _camelot,
        _             => null
    };
}
