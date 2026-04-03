namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>DATA_GAP_FILLED</c> — emitted by data-layer when a backfill job
/// successfully closes a detected gap.
/// </summary>
/// <param name="Exchange">Exchange where the gap was filled.</param>
/// <param name="Symbol">Affected trading symbol.</param>
/// <param name="Timeframe">Candle timeframe that was backfilled.</param>
/// <param name="GapStart">Start of the filled window (UTC, inclusive).</param>
/// <param name="GapEnd">End of the filled window (UTC, exclusive).</param>
/// <param name="CandlesInserted">Number of candles inserted by the backfill.</param>
/// <param name="DurationMs">Wall-clock duration of the backfill operation in milliseconds.</param>
public sealed record DataGapFilledPayload(
    string Exchange,
    string Symbol,
    string Timeframe,
    DateTimeOffset GapStart,
    DateTimeOffset GapEnd,
    int CandlesInserted,
    long DurationMs);
