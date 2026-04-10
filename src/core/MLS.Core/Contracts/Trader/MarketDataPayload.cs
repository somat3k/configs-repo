using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Trader;

/// <summary>
/// Payload for <c>MARKET_DATA_UPDATE</c> envelopes.
/// Carries a market price snapshot and pre-computed technical indicators for a single symbol.
/// </summary>
/// <param name="Symbol">Normalised trading symbol, e.g. <c>BTC-USDT</c>.</param>
/// <param name="Price">Current mid-market price in quote currency.</param>
/// <param name="Rsi">Relative Strength Index (14-period), range [0, 100].</param>
/// <param name="MacdValue">MACD line value (12-period EMA − 26-period EMA).</param>
/// <param name="MacdSignal">MACD signal line value (9-period EMA of the MACD line).</param>
/// <param name="BollingerUpper">Bollinger Band upper rail (20-period SMA + 2σ).</param>
/// <param name="BollingerMiddle">Bollinger Band middle rail (20-period SMA).</param>
/// <param name="BollingerLower">Bollinger Band lower rail (20-period SMA − 2σ).</param>
/// <param name="VolumeDelta">Buy-side minus sell-side volume over the current bar.</param>
/// <param name="Momentum">Rate of price change over the lookback period.</param>
/// <param name="AtrValue">Average True Range (14-period) — used for stop-loss calculation.</param>
/// <param name="Timestamp">UTC timestamp when this snapshot was produced.</param>
public sealed record MarketDataPayload(
    [property: JsonPropertyName("symbol")]           string         Symbol,
    [property: JsonPropertyName("price")]            decimal        Price,
    [property: JsonPropertyName("rsi")]              float          Rsi,
    [property: JsonPropertyName("macd_value")]       float          MacdValue,
    [property: JsonPropertyName("macd_signal")]      float          MacdSignal,
    [property: JsonPropertyName("bollinger_upper")]  float          BollingerUpper,
    [property: JsonPropertyName("bollinger_middle")] float          BollingerMiddle,
    [property: JsonPropertyName("bollinger_lower")]  float          BollingerLower,
    [property: JsonPropertyName("volume_delta")]     float          VolumeDelta,
    [property: JsonPropertyName("momentum")]         float          Momentum,
    [property: JsonPropertyName("atr_value")]        float          AtrValue,
    [property: JsonPropertyName("timestamp")]        DateTimeOffset Timestamp);
