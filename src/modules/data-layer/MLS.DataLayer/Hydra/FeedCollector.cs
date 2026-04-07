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

    private const int FlushBatchSize  = 100;   // flush after this many candles
    private const int FlushIntervalMs = 500;   // or after this many milliseconds

    /// <summary>Identifies the exchange this collector serves, e.g. <c>hyperliquid</c>.</summary>
    public abstract string ExchangeId { get; }

    /// <summary>
    /// Runs the feed collection loop until <paramref name="ct"/> is cancelled.
    /// On disconnect the loop reconnects using exponential backoff with jitter.
    /// Received candles are buffered and flushed to <paramref name="repository"/> in
    /// batches (up to <see cref="FlushBatchSize"/> candles or every <see cref="FlushIntervalMs"/> ms).
    /// </summary>
    /// <param name="key">Feed subscription key (exchange / symbol / timeframe).</param>
    /// <param name="repository">Candle persistence repository (scoped lifetime).</param>
    /// <param name="ct">Cancellation token provided by <see cref="FeedScheduler"/>.</param>
    public async Task RunAsync(FeedKey key, CandleRepository repository, CancellationToken ct)
    {
        var attempt = 0;

        var safeExchange  = HydraUtils.SanitiseFeedId(key.Exchange);
        var safeSymbol    = HydraUtils.SanitiseFeedId(key.Symbol);
        var safeTimeframe = HydraUtils.SanitiseFeedId(key.Timeframe);

        while (!ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] connecting (attempt {N})",
                safeExchange, safeSymbol, safeTimeframe, attempt + 1);

            bool connected = false;
            var buffer     = new List<CandleEntity>(FlushBatchSize);
            var nextFlush  = DateTime.UtcNow.AddMilliseconds(FlushIntervalMs);

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
                            safeExchange, safeSymbol, safeTimeframe);
                    }

                    buffer.Add(candle);

                    // Flush when buffer is full or the flush interval has elapsed
                    if (buffer.Count >= FlushBatchSize || DateTime.UtcNow >= nextFlush)
                    {
                        await repository.UpsertBatchAsync(buffer, ct).ConfigureAwait(false);
                        buffer.Clear();
                        nextFlush = DateTime.UtcNow.AddMilliseconds(FlushIntervalMs);
                    }
                }

                // Flush any remaining candles after the stream ends
                if (buffer.Count > 0)
                    await repository.UpsertBatchAsync(buffer, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Flush before graceful shutdown
                if (buffer.Count > 0)
                {
                    try { await repository.UpsertBatchAsync(buffer, CancellationToken.None).ConfigureAwait(false); }
                    catch { /* best-effort */ }
                }
                return;
            }
            catch (Exception ex)
            {
                // Flush partial buffer before reconnect attempt
                if (buffer.Count > 0)
                {
                    try { await repository.UpsertBatchAsync(buffer, CancellationToken.None).ConfigureAwait(false); }
                    catch { /* best-effort */ }
                }

                _logger.LogWarning(ex,
                    "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] stream error (attempt {N})",
                    safeExchange, safeSymbol, safeTimeframe, attempt + 1);
            }

            if (ct.IsCancellationRequested) return;

            var delay = GetBackoff(attempt++);
            _logger.LogInformation(
                "FeedCollector [{Exchange}/{Symbol}/{Timeframe}] reconnecting in {Delay}",
                safeExchange, safeSymbol, safeTimeframe, delay);

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
