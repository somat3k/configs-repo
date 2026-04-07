using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace MLS.DataLayer.FeatureStore;

// ── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>
/// Identifies which model a feature vector is computed for.
/// </summary>
public enum ModelType
{
    /// <summary>Trading model (<c>model-t</c>) — used by the <c>trader</c> module.</summary>
    Trading = 0,

    /// <summary>Arbitrage model (<c>model-a</c>) — used by the <c>arbitrager</c> module.</summary>
    Arbitrage = 1,

    /// <summary>DeFi model (<c>model-d</c>) — used by the <c>defi</c> module.</summary>
    DeFi = 2,
}

// ── Input candle ──────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight OHLCV candle value type used as input to <see cref="FeatureEngineer"/>.
/// </summary>
/// <param name="Open">Open price.</param>
/// <param name="High">High price.</param>
/// <param name="Low">Low price.</param>
/// <param name="Close">Close price.</param>
/// <param name="Volume">Base-asset volume.</param>
public readonly record struct OhlcvCandle(
    double Open,
    double High,
    double Low,
    double Close,
    double Volume);

// ── Feature vectors ───────────────────────────────────────────────────────────

/// <summary>
/// 8-feature vector computed for the <b>Trading</b> model (<c>model-t</c>).
/// </summary>
/// <remarks>
/// Schema version 1 defines the following ordered features:
/// <list type="number">
///   <item><see cref="Rsi14"/> — RSI(14), range [0, 100].</item>
///   <item><see cref="MacdSignal"/> — MACD signal line, normalised by close price.</item>
///   <item><see cref="BbPosition"/> — Price position within Bollinger Bands(20,2), range [0, 1].</item>
///   <item><see cref="VolumeDelta"/> — Relative volume change vs prior candle (%).</item>
///   <item><see cref="Momentum20"/> — 20-period price momentum (close[n]/close[n-20] − 1).</item>
///   <item><see cref="AtrNormalised"/> — ATR(14) divided by close price.</item>
///   <item><see cref="SpreadBps"/> — (High−Low)/Close × 10 000 (basis-point candle range).</item>
///   <item><see cref="VwapDistance"/> — (Close−VWAP)/VWAP over the computation window.</item>
/// </list>
/// </remarks>
/// <param name="Rsi14">Relative Strength Index over 14 periods.</param>
/// <param name="MacdSignal">MACD signal line value (normalised by close).</param>
/// <param name="BbPosition">Bollinger Band position (0 = lower band, 1 = upper band).</param>
/// <param name="VolumeDelta">Relative volume change as a decimal fraction.</param>
/// <param name="Momentum20">20-bar price momentum as a decimal fraction.</param>
/// <param name="AtrNormalised">ATR(14) divided by close price.</param>
/// <param name="SpreadBps">Candle high-low range in basis points.</param>
/// <param name="VwapDistance">Fractional distance of close from VWAP.</param>
/// <param name="ModelType">Model this vector was computed for.</param>
/// <param name="SchemaVersion">Feature schema version — must match model input contract.</param>
public sealed record FeatureVector(
    double Rsi14,
    double MacdSignal,
    double BbPosition,
    double VolumeDelta,
    double Momentum20,
    double AtrNormalised,
    double SpreadBps,
    double VwapDistance,
    ModelType ModelType,
    int SchemaVersion)
{
    /// <summary>
    /// Returns the feature values as a fixed-size double array in schema order.
    /// </summary>
    public double[] ToArray() =>
    [
        Rsi14,
        MacdSignal,
        BbPosition,
        VolumeDelta,
        Momentum20,
        AtrNormalised,
        SpreadBps,
        VwapDistance,
    ];

    /// <summary>Number of features in this vector.</summary>
    public const int FeatureCount = 8;
}

// ── Feature store entity ──────────────────────────────────────────────────────

/// <summary>
/// Persisted feature vector row in the <c>feature_store_vectors</c> PostgreSQL table.
/// </summary>
/// <remarks>
/// A separate <c>feature_store</c> table (from the platform bootstrap SQL) holds
/// feature-set metadata (name, version, schema JSONB, IPFS ref).
/// This table stores computed per-candle feature vectors.
/// </remarks>
[Table("feature_store_vectors")]
public sealed class FeatureStoreEntity
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Exchange identifier, e.g. <c>hyperliquid</c>.</summary>
    [Required]
    [MaxLength(64)]
    [Column("exchange")]
    public string Exchange { get; set; } = string.Empty;

    /// <summary>Normalised trading symbol, e.g. <c>BTC-USDT</c>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Candle timeframe, e.g. <c>1h</c>.</summary>
    [Required]
    [MaxLength(8)]
    [Column("timeframe")]
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>Target model type.</summary>
    [Required]
    [MaxLength(16)]
    [Column("model_type")]
    public string ModelType { get; set; } = string.Empty;

    /// <summary>
    /// Feature schema version — must match the ONNX model's expected input dimensions.
    /// Increment when the feature set changes.
    /// </summary>
    [Column("schema_version")]
    public int SchemaVersion { get; set; }

    /// <summary>
    /// Timestamp of the last candle included in this feature vector (UTC).
    /// Matches <c>CandleEntity.OpenTime</c> of the most-recent candle in the window.
    /// </summary>
    [Column("feature_timestamp")]
    public DateTimeOffset FeatureTimestamp { get; set; }

    /// <summary>
    /// Feature values serialised as a JSON array in schema order.
    /// </summary>
    [Required]
    [Column("features_json", TypeName = "jsonb")]
    public string FeaturesJson { get; set; } = "[]";

    /// <summary>Row insert / compute timestamp (UTC).</summary>
    [Column("computed_at")]
    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ── Schema version constants ──────────────────────────────────────────────────

/// <summary>
/// Current schema version constants per model type.
/// Increment the relevant version whenever the corresponding feature set changes.
/// </summary>
public static class FeatureSchemaVersions
{
    /// <summary>Current schema version for <see cref="ModelType.Trading"/> (model-t).</summary>
    public const int Trading = 1;

    /// <summary>Current schema version for <see cref="ModelType.Arbitrage"/> (model-a).</summary>
    public const int Arbitrage = 1;

    /// <summary>Current schema version for <see cref="ModelType.DeFi"/> (model-d).</summary>
    public const int DeFi = 1;
}

// ── Canonical model identifiers ───────────────────────────────────────────────

/// <summary>
/// Maps <see cref="ModelType"/> enum values to their canonical cross-module string
/// identifiers (<c>model-t</c>, <c>model-a</c>, <c>model-d</c>).
/// </summary>
/// <remarks>
/// Always use these identifiers when persisting model type strings to the database
/// or sending them in envelope payloads, to stay consistent with the rest of the
/// MLS platform (designer, ml-runtime, ai-hub, etc.).
/// </remarks>
public static class ModelTypeIds
{
    /// <summary>Canonical identifier for <see cref="ModelType.Trading"/>.</summary>
    public const string Trading = "model-t";

    /// <summary>Canonical identifier for <see cref="ModelType.Arbitrage"/>.</summary>
    public const string Arbitrage = "model-a";

    /// <summary>Canonical identifier for <see cref="ModelType.DeFi"/>.</summary>
    public const string DeFi = "model-d";

    /// <summary>
    /// Returns the canonical cross-module identifier for <paramref name="modelType"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown for an unrecognised <see cref="ModelType"/> value.
    /// </exception>
    public static string For(ModelType modelType) => modelType switch
    {
        ModelType.Trading   => Trading,
        ModelType.Arbitrage => Arbitrage,
        ModelType.DeFi      => DeFi,
        _                   => throw new ArgumentOutOfRangeException(
                                   nameof(modelType), modelType,
                                   "Unknown ModelType — add a canonical ID mapping."),
    };
}

// ── Chart-ready plot sample ───────────────────────────────────────────────────

/// <summary>
/// A single chart-ready data point projected from a <see cref="FeatureVector"/>.
/// </summary>
/// <remarks>
/// Designed for direct use with
/// <c>canvas-interop.js:updateApexSeries(chartId, SeriesName, TimestampEpochMs, Value)</c>
/// and <c>initIndicatorChart</c>. Consumers iterate the list returned by
/// <see cref="FeatureEngineer.ToPlotSamples"/> and call <c>updateApexSeries</c>
/// once per sample to push a new point to the live chart.
/// </remarks>
/// <param name="IndicatorId">
/// Unique slug matching <see cref="IndicatorDescriptor.Id"/> (e.g. <c>"rsi_14"</c>).
/// Use this to look up rendering metadata from <see cref="IndicatorLibrary"/>.
/// </param>
/// <param name="SeriesName">
/// Human-readable series name for the chart legend (e.g. <c>"RSI (14)"</c>).
/// Matches the <c>seriesName</c> argument in <c>updateApexSeries</c>.
/// </param>
/// <param name="TimestampEpochMs">
/// Unix epoch milliseconds used as the x-axis value in ApexCharts datetime series.
/// </param>
/// <param name="Value">Computed indicator value (y-axis).</param>
public sealed record IndicatorPlotSample(
    string IndicatorId,
    string SeriesName,
    long   TimestampEpochMs,
    double Value);
