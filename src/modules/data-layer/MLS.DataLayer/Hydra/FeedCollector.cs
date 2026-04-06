using MLS.DataLayer.Persistence;

namespace MLS.DataLayer.Hydra;

/// <summary>
/// Describes a single active feed subscription managed by the <see cref="FeedScheduler"/>.
/// </summary>
/// <param name="Exchange">Exchange identifier, e.g. <c>hyperliquid</c>.</param>
/// <param name="Symbol">Normalised trading symbol, e.g. <c>BTC-USDT</c>.</param>
/// <param name="Timeframe">Candle timeframe, e.g. <c>1m</c>, <c>1h</c>.</param>
public sealed record FeedKey(string Exchange, string Symbol, string Timeframe);

/// <summary>
/// Abstract base class for all exchange feed collectors.
/// Implements the reconnect loop with exponential backoff and delegates
/// the per-exchange connection logic to the concrete subclass via
/// <see cref="StreamCandlesAsync"/>.
/// </summary>
public abstract class FeedCollector(ILogger _logger)
{
    // ── Exponential backoff (base 1 s → max 60 s, with jitter) ───────────────
    private static readonly TimeSpan[] BackoffDelays =
        Enumerable.Range(0, 10)
            .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
            .ToArray();

    /// <summary>Identifies the exchange this collector serves, e.g. <c>hyperliquid</c>.</summary>
    public abstract string ExchangeId { get; }

    /// <summary>
    /// Runs the feed collection loop until <paramref name="ct"/> is cancelled.
    /// On disconnect the loop reconnects using exponential backoff with jitter.
    /// Each successfully received candle is persisted via <paramref name="repository"/>.
    /// </summary>
    /// <param name="key">Feed subscription key (exchange / symbol / timeframe).</param>
    /// <param name="repository">Candle persistence repository (scoped lifetime).</param>
    /// <param name="ct">Cancellation token provided by <see cref="FeedScheduler"/>.</param>
    public async Task RunAsync(FeedKey key, CandleRepository repository, CancellationToken ct)
    {
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] connecting (attempt {N})",
                key.Exchange, key.Symbol, key.Timeframe, attempt + 1);

            bool connected = false;
            try
            {
                await foreach (var candle in StreamCandlesAsync(key, ct).ConfigureAwait(false))
                {
                    if (!connected)
                    {
                        connected = true;
                        attempt   = 0; // reset backoff on successful first yield
                        _logger.LogInformation(
                            "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] stream active",
                            key.Exchange, key.Symbol, key.Timeframe);
                    }

                    await repository.UpsertBatchAsync([candle], ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Graceful shutdown — exit immediately
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] stream error (attempt {N})",
                    key.Exchange, key.Symbol, key.Timeframe, attempt + 1);
            }

            if (ct.IsCancellationRequested) return;

            var delay = GetBackoff(attempt++);
            _logger.LogInformation(
                "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] reconnecting in {Delay}",
                key.Exchange, key.Symbol, key.Timeframe, delay);

            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Streams candles from the exchange until the connection drops or <paramref name="ct"/>
    /// is cancelled.  Implementations should yield each received candle as soon as it arrives;
    /// when the connection closes the enumeration must complete (not throw, unless cancelled).
    /// </summary>
    protected abstract IAsyncEnumerable<CandleEntity> StreamCandlesAsync(
        FeedKey key, CancellationToken ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
        var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return baseDelay + jitter;
    }
}
