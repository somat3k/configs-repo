using System.Text.Json.Serialization;

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
    [property: JsonPropertyName("exchange")]         string Exchange,
    [property: JsonPropertyName("symbol")]           string Symbol,
    [property: JsonPropertyName("timeframe")]        string Timeframe,
    [property: JsonPropertyName("gap_start")]        DateTimeOffset GapStart,
    [property: JsonPropertyName("gap_end")]          DateTimeOffset GapEnd,
    [property: JsonPropertyName("candles_inserted")] int CandlesInserted,
    [property: JsonPropertyName("duration_ms")]      long DurationMs);
