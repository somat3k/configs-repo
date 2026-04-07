using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using MLS.DataLayer.Configuration;
using MLS.DataLayer.Persistence;
using MLS.DataLayer.Services;

namespace MLS.DataLayer.Hydra;

/// <summary>
/// Periodic background service that detects missing candle ranges (gaps) in
/// the stored data for each active feed subscription.
/// </summary>
/// <remarks>
/// <para>
/// Algorithm (runs every <see cref="DataLayerOptions.GapDetectorIntervalSeconds"/> seconds):
/// </para>
/// <list type="number">
///   <item>For each <see cref="FeedKey"/> in <see cref="FeedScheduler.ActiveFeeds"/>:</item>
///   <item>Query <c>MAX(open_time)</c> from the candles table.</item>
///   <item>Compute <c>expected_count = (now - latest_stored) / timeframe_seconds</c>.</item>
///   <item>Query <c>COUNT(*) WHERE open_time &gt; latest_stored</c>.</item>
///   <item>If <c>actual_count &lt; expected_count × 0.95</c>, emit
///         <c>DATA_GAP_DETECTED</c> and enqueue a backfill job.</item>
/// </list>
/// </remarks>
public sealed class GapDetector(
    IOptions<DataLayerOptions> _options,
    FeedScheduler _scheduler,
    BackfillPipeline _backfill,
    IDbContextFactory<DataLayerDbContext> _dbFactory,
    IEnvelopeSender _envelopeSender,
    ILogger<GapDetector> _logger) : BackgroundService
{
    private const string ModuleId = "data-layer";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(1, _options.Value.GapDetectorIntervalSeconds));

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            await DetectGapsAsync(ct).ConfigureAwait(false);
        }
    }

    // ── Core detection ────────────────────────────────────────────────────────

    private async Task DetectGapsAsync(CancellationToken ct)
    {
        var feeds = _scheduler.ActiveFeeds();
        if (feeds.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var now            = DateTimeOffset.UtcNow;

        foreach (var key in feeds)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await CheckFeedAsync(db, key, now, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "GapDetector: error checking [{Exchange}/{Symbol}/{Timeframe}]",
                    HydraUtils.SanitiseFeedId(key.Exchange),
                    HydraUtils.SanitiseFeedId(key.Symbol),
                    HydraUtils.SanitiseFeedId(key.Timeframe));
            }
        }
    }

    private async Task CheckFeedAsync(
        DataLayerDbContext db, FeedKey key, DateTimeOffset now, CancellationToken ct)
    {
        var latestStored = await db.Candles
            .Where(c => c.Exchange == key.Exchange
                     && c.Symbol    == key.Symbol
                     && c.Timeframe == key.Timeframe)
            .MaxAsync(c => (DateTimeOffset?)c.OpenTime, ct)
            .ConfigureAwait(false);

        if (latestStored is null)
        {
            // No data at all — nothing to compare against yet
            _logger.LogDebug(
                "GapDetector: no candles for [{Exchange}/{Symbol}/{Timeframe}] — skipping",
                HydraUtils.SanitiseFeedId(key.Exchange),
                HydraUtils.SanitiseFeedId(key.Symbol),
                HydraUtils.SanitiseFeedId(key.Timeframe));
            return;
        }

        var elapsed       = now - latestStored.Value;
        var tfSeconds     = HydraUtils.TimeframeToSeconds(key.Timeframe);

        // How many candles are missing in the gap between latestStored and now.
        // Subtract 1 so that the latest stored candle itself is not counted as missing.
        var missingCount  = (int)(elapsed.TotalSeconds / tfSeconds) - 1;

        // No gap if fewer than 1.05 × interval have passed (5 % tolerance)
        if (missingCount <= 0) return;

        var gapStart = CalculateGapStart(latestStored.Value, tfSeconds);
        var gapEnd   = now;

        _logger.LogWarning(
            "GapDetector: gap detected [{Exchange}/{Symbol}/{Timeframe}] " +
            "missing={Missing} start={GapStart} end={GapEnd}",
            HydraUtils.SanitiseFeedId(key.Exchange),
            HydraUtils.SanitiseFeedId(key.Symbol),
            HydraUtils.SanitiseFeedId(key.Timeframe),
            missingCount, gapStart, gapEnd);

        // Broadcast DATA_GAP_DETECTED envelope to Block Controller
        var payload = new DataGapDetectedPayload(
            Exchange:       key.Exchange,
            Symbol:         key.Symbol,
            Timeframe:      key.Timeframe,
            GapStart:       gapStart,
            GapEnd:         gapEnd,
            MissingCandles: missingCount);

        var envelope = EnvelopePayload.Create(
            MessageTypes.DataGapDetected, ModuleId, payload);

        await _envelopeSender.SendEnvelopeAsync(envelope, ct).ConfigureAwait(false);

        // Enqueue backfill job
        var gap = new GapRange(key, gapStart, gapEnd, missingCount);
        await _backfill.EnqueueAsync(gap, ct).ConfigureAwait(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the start of the gap — the first missing candle time,
    /// which is one timeframe interval after the last stored candle.
    /// </summary>
    private static DateTimeOffset CalculateGapStart(DateTimeOffset latestStored, double tfSeconds)
        => latestStored.AddSeconds(tfSeconds);
}
