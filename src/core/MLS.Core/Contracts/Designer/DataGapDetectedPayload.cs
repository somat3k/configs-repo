using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>DATA_GAP_DETECTED</c> — emitted by data-layer when a gap
/// is detected in a collected feed.
/// </summary>
/// <param name="Exchange">Exchange where the gap was detected.</param>
/// <param name="Symbol">Affected trading symbol.</param>
/// <param name="Timeframe">Candle timeframe with the gap.</param>
/// <param name="GapStart">Start of the missing data window (UTC, inclusive).</param>
/// <param name="GapEnd">End of the missing data window (UTC, exclusive).</param>
/// <param name="MissingCandles">Number of missing candles.</param>
public sealed record DataGapDetectedPayload(
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("timeframe")] string Timeframe,
    [property: JsonPropertyName("gap_start")] DateTimeOffset GapStart,
    [property: JsonPropertyName("gap_end")] DateTimeOffset GapEnd,
    [property: JsonPropertyName("missing_candles")] int MissingCandles);
