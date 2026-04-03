---
name: hydra-collector
source: custom (MLS Trading Platform)
description: 'Hydra data collection patterns — feed collector lifecycle, gap detection algorithm, backfill pipeline, feature store schema, and real-time candle routing to designer blocks.'
---

# Hydra Data Collector — MLS Trading Platform

> Apply this skill when working on: `MLS.DataLayer.Hydra`, feed collectors, gap detection, backfill pipeline, feature engineering, or `DataHydra` blocks in the designer.

---

## Feed Collector Rules

```csharp
// 1. Extend FeedCollector base class — don't implement BackgroundService directly
// 2. Use bounded Channel<OHLCVCandle>(4096) with DropOldest for back-pressure
// 3. Implement exponential backoff: base 1s, max 60s, jitter (prevent thundering herd)
// 4. ALWAYS upsert (INSERT ... ON CONFLICT DO NOTHING) — never assume no duplicates
// 5. Update Redis cache AFTER PostgreSQL write (eventual consistency, cache is L1 read path)
// 6. Emit CANDLE_STREAM envelope for each candle to Block Controller
```

## FeedCollector Base Class

```csharp
public abstract class FeedCollector : BackgroundService
{
    private readonly Channel<OHLCVCandle> _buffer =
        Channel.CreateBounded<OHLCVCandle>(new BoundedChannelOptions(4096)
        {
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    protected abstract Task ConnectAsync(CancellationToken ct);
    protected abstract IAsyncEnumerable<OHLCVCandle> ReadCandlesAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(ct);
                attempt = 0;
                await foreach (var candle in ReadCandlesAsync(ct))
                    await _buffer.Writer.WriteAsync(candle, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feed error, attempt {Attempt}", ++attempt);
                await Task.Delay(GetBackoff(attempt), ct);
            }
        }
    }
}
```

---

## Gap Detection Rules

```csharp
// Run on 1-minute timer via BackgroundService
// Tolerance: 5% (if actual < expected * 0.95, gap detected)
// Use Parallel.ForEachAsync over all active (exchange, symbol, timeframe) combinations
// On gap detection:
//   1. Emit DATA_GAP_DETECTED envelope to Block Controller
//   2. Enqueue to BackfillPipeline (Channel<DataGap>)
// On backfill complete:
//   1. Emit DATA_GAP_FILLED envelope
```

## Gap Detection Implementation

```csharp
public sealed class GapDetector(
    IRepository<OHLCVCandle> _repo,
    IBlockControllerClient _bus,
    IBackfillPipeline _backfill,
    ILogger<GapDetector> _logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var feeds = await _repo.GetActiveFeedsAsync(ct);
            await Parallel.ForEachAsync(feeds, ct, async (feed, ct) =>
            {
                var (expected, actual) = await _repo.GetCandleCountAsync(
                    feed.Exchange, feed.Symbol, feed.Timeframe,
                    DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, ct);

                if (actual < expected * 0.95)
                {
                    var gap = new DataGap(feed.Exchange, feed.Symbol, feed.Timeframe,
                        missing: expected - actual);

                    await _bus.SendAsync(EnvelopePayload.Create(
                        MessageTypes.DataGapDetected, "data-layer",
                        new DataGapDetectedPayload(...)), ct);

                    await _backfill.EnqueueAsync(gap, ct);
                }
            });
        }
    }
}
```

---

## Backfill Pipeline Rules

```csharp
// Max concurrent backfill jobs: 4 (configurable)
// Rate limit: 5 REST API calls/second per exchange (configurable)
// Chunk size: 1000 candles per REST request (exchange-dependent)
// Retry: 3 attempts with exponential backoff
// After 3 failures: emit DATA_GAP_FAILED envelope with reason
```

## Backfill Pipeline Implementation

```csharp
public sealed class BackfillPipeline : IBackfillPipeline, IAsyncDisposable
{
    private readonly Channel<DataGap> _queue =
        Channel.CreateBounded<DataGap>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.Wait, // Backpressure — gap detection pauses
        });

    private readonly SemaphoreSlim _concurrencyLimit;  // Count injected from BackfillOptions.MaxConcurrentJobs
    // Constructor: _concurrencyLimit = new(options.MaxConcurrentJobs, options.MaxConcurrentJobs);

    public async ValueTask EnqueueAsync(DataGap gap, CancellationToken ct) =>
        await _queue.Writer.WriteAsync(gap, ct);

    private async Task ProcessAsync(DataGap gap, CancellationToken ct)
    {
        await _concurrencyLimit.WaitAsync(ct);
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var candles = await _adapter.FetchHistoricalAsync(
                        gap.Exchange, gap.Symbol, gap.Timeframe,
                        gap.GapStart, gap.GapEnd, chunkSize: 1000, ct);

                    await _repo.UpsertBatchAsync(candles, ct);
                    await _bus.SendAsync(BuildGapFilledEnvelope(gap, candles.Count), ct);
                    return;
                }
                catch (Exception ex) when (attempt < 3)
                {
                    _logger.LogWarning(ex, "Backfill attempt {Attempt} failed for {Gap}", attempt, gap);
                    await Task.Delay(GetBackoff(attempt), ct);
                }
            }
            await _bus.SendAsync(BuildGapFailedEnvelope(gap, "Max retries exceeded"), ct);
        }
        finally
        {
            _concurrencyLimit.Release();
        }
    }
}
```

---

## Feature Engineering Rules

```csharp
// FeatureEngineer MUST use System.Numerics.Vector<float> for vectorised computation
// Target: < 1ms for 200-candle window (L1 acceleration)
// Feature values MUST match Python reference implementation to 6 decimal places
// Store computed features in feature_store table with schema_version
// Schema version MUST match model's expected input dimension
// NEVER use Python for production inference feature computation (only C# ONNX path)
```

## Feature Engineering with SIMD

```csharp
public static class FeatureEngineer
{
    /// <summary>Compute normalised RSI using SIMD (< 0.1ms for 200-candle window).</summary>
    public static float ComputeRsi(ReadOnlySpan<float> closes, int period = 14)
    {
        // 1. Compute price deltas
        Span<float> deltas = stackalloc float[closes.Length - 1];
        for (var i = 0; i < deltas.Length; i++)
            deltas[i] = closes[i + 1] - closes[i];

        // 2. Separate gains and losses using SIMD
        var vectorSize = Vector<float>.Count;
        float avgGain = 0, avgLoss = 0;

        for (var i = 0; i < period; i++)
        {
            var d = deltas[i];
            if (d > 0) avgGain += d;
            else       avgLoss -= d;
        }
        avgGain /= period;
        avgLoss /= period;

        // 3. Wilder smoothing for remaining deltas
        for (var i = period; i < deltas.Length; i++)
        {
            var d = deltas[i];
            avgGain = (avgGain * (period - 1) + (d > 0 ? d : 0)) / period;
            avgLoss = (avgLoss * (period - 1) + (d < 0 ? -d : 0)) / period;
        }

        // 4. RSI = 100 - (100 / (1 + RS))
        var rs = avgLoss == 0 ? float.MaxValue : avgGain / avgLoss;
        return 100f - 100f / (1f + rs);
    }
}
```

---

## FeatureStore Query Pattern

```csharp
// Load features for training (called by DataLoaderBlock)
// Use compiled EF Core query for performance
// Arrow feather format for large batch export to IPFS/training
var features = await _db.FeatureStore
    .Where(f => f.Exchange == exchange && f.Symbol == symbol
                && f.ModelType == modelType && f.SchemaVersion == schemaVersion
                && f.OpenTime >= from && f.OpenTime < to)
    .OrderBy(f => f.OpenTime)
    .AsNoTracking()
    .ToArrayAsync(ct);
```

---

## Data Flow to Designer Canvas

```
FeedCollector receives candle
  → Persist to PostgreSQL (upsert)
  → Update Redis latest_candle:{exchange}:{symbol}:{timeframe}  (TTL = timeframe duration)
  → Emit CANDLE_STREAM envelope
    → Block Controller routes to subscribed CandleFeedBlock group
      → CandleFeedBlock.ProcessAsync(candle)
        → Output on candle_output socket
          → Connected IndicatorBlocks process (RSI, MACD, Bollinger...)
            → Connected MLSignal blocks inference
              → TRADE_SIGNAL emitted
                → Broker / Trader module receives
```

---

## Redis Cache Schema

```
Key pattern:       latest_candle:{exchange}:{symbol}:{timeframe}
TTL:               matches candle timeframe (e.g. 60s for 1m, 300s for 5m) — refreshed on each write
Value:             JSON-encoded OHLCVCandle
Feature cache:     features:{exchange}:{symbol}:{schema_version}:{timestamp}
TTL:               5 minutes (feature vectors are stable within a window)
```

---

## Testing Requirements

```csharp
// 1. Gap detection must trigger for feeds where actual < expected * 0.95
// 2. Backfill pipeline must respect max 4 concurrent jobs
// 3. Feature values must match Python reference to 6 decimal places
// 4. Upsert must not throw on duplicate candle insertion

[Fact]
public async Task GapDetector_WhenActualBelowThreshold_EmitsDataGapDetected()
{
    _repo.Setup(r => r.GetCandleCountAsync(...))
         .ReturnsAsync((expected: 60, actual: 55)); // 55/60 = 91.7% < 95%

    await _detector.RunOnceAsync(CancellationToken.None);

    _bus.Verify(b => b.SendAsync(
        It.Is<EnvelopePayload>(e => e.Type == MessageTypes.DataGapDetected),
        It.IsAny<CancellationToken>()), Times.Once);
}
```
