using System.Text.Json.Serialization;

namespace MLS.WorkflowDemo.Models;

// ── Hyperliquid public-API shapes ─────────────────────────────────────────────

/// <summary>Mid price entry from Hyperliquid <c>allMids</c>.</summary>
public sealed record AssetMid(string Symbol, decimal Mid);

/// <summary>Universe asset metadata from <c>metaAndAssetCtxs</c> index 0.</summary>
public sealed record UniverseAsset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("szDecimals")] int SzDecimals,
    [property: JsonPropertyName("maxLeverage")] int MaxLeverage);

/// <summary>Per-asset market context from <c>metaAndAssetCtxs</c> index 1.</summary>
public sealed record AssetCtx(
    [property: JsonPropertyName("dayNtlVlm")]   string DayNtlVlm,
    [property: JsonPropertyName("prevDayPx")]   string PrevDayPx,
    [property: JsonPropertyName("markPx")]      string? MarkPx,
    [property: JsonPropertyName("midPx")]       string? MidPx,
    [property: JsonPropertyName("funding")]     string Funding,
    [property: JsonPropertyName("openInterest")]string OpenInterest);

/// <summary>Merged view: asset metadata + live market context.</summary>
public sealed record AssetMarket(
    string Symbol,
    decimal Mid,
    decimal PrevDay,
    decimal DayVolumeUsd,
    decimal Funding,
    decimal OpenInterest,
    int MaxLeverage)
{
    public decimal ChangePercent =>
        PrevDay == 0m ? 0m : Math.Round((Mid - PrevDay) / PrevDay * 100m, 2);
}

/// <summary>One OHLCV candle from Hyperliquid <c>candleSnapshot</c>.</summary>
public sealed record Candle(
    [property: JsonPropertyName("T")] long CloseTime,
    [property: JsonPropertyName("t")] long OpenTime,
    [property: JsonPropertyName("o")] string Open,
    [property: JsonPropertyName("h")] string High,
    [property: JsonPropertyName("l")] string Low,
    [property: JsonPropertyName("c")] string Close,
    [property: JsonPropertyName("v")] string Volume,
    [property: JsonPropertyName("n")] int Trades)
{
    public decimal O => decimal.Parse(Open, System.Globalization.CultureInfo.InvariantCulture);
    public decimal H => decimal.Parse(High, System.Globalization.CultureInfo.InvariantCulture);
    public decimal L => decimal.Parse(Low, System.Globalization.CultureInfo.InvariantCulture);
    public decimal C => decimal.Parse(Close, System.Globalization.CultureInfo.InvariantCulture);
    public decimal V => decimal.Parse(Volume, System.Globalization.CultureInfo.InvariantCulture);
    public DateTimeOffset OpenDt => DateTimeOffset.FromUnixTimeMilliseconds(OpenTime);
}

// ── DeFi Llama API shapes ─────────────────────────────────────────────────────

/// <summary>Protocol entry from DeFi Llama <c>/v2/protocols</c>.</summary>
public sealed record DefiProtocol(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("tvl")]      double? Tvl,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("change_7d")]double? Change7d,
    [property: JsonPropertyName("chains")]   List<string>? Chains);

// ── Workflow pipeline step ────────────────────────────────────────────────────

/// <summary>Describes a single step in a module's functional pipeline.</summary>
public sealed record PipelineStep(
    string Label,
    string Icon,
    string Description,
    string Color = "#00d4ff");

/// <summary>Computed feature vector produced by the DataLayer FeatureEngineer.</summary>
public sealed record FeatureVector(
    string Symbol,
    decimal Rsi14,
    decimal MacdSignal,
    decimal BbPosition,
    decimal VolumeDelta,
    decimal Momentum20,
    decimal AtrNorm,
    decimal SpreadBps,
    decimal VwapDistance);

/// <summary>Arbitrage opportunity derived from price differentials across venues.</summary>
public sealed record ArbOpportunity(
    string Symbol,
    string BuyVenue,
    decimal BuyPrice,
    string SellVenue,
    decimal SellPrice,
    decimal SpreadBps,
    decimal EstGasUsd,
    decimal NetProfitUsd);
