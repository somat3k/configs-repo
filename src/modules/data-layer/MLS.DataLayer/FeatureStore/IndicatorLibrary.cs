namespace MLS.DataLayer.FeatureStore;

/// <summary>
/// Static catalog of all technical indicators available in the MLS feature
/// engineering pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The library serves as a browsable "compendium" of available indicator options.
/// Consumers (UI components, strategy blocks, chart panels) can call
/// <see cref="GetAll"/>, filter by <see cref="GetByModelType"/>, or perform a
/// direct lookup with <see cref="TryGet"/>.
/// </para>
/// <para>
/// Indicators whose <see cref="IndicatorDescriptor.FeatureIndex"/> is ≥ 0 are
/// wired into the current <see cref="FeatureVector"/> schema for their listed
/// model types and are directly producible by <see cref="FeatureEngineer.ToPlotSamples"/>.
/// Indicators with <c>FeatureIndex == -1</c> are registered as library options
/// available for future model expansion or custom strategy blocks in the Designer.
/// </para>
/// </remarks>
public static class IndicatorLibrary
{
    // ── Catalog ───────────────────────────────────────────────────────────────

    private static readonly IndicatorDescriptor[] _all =
    [
        // ── model-t / model-a / model-d shared feature vector (indices 0–7) ──

        new(
            Id:           "rsi_14",
            Name:         "RSI (14)",
            Description:  "Wilder-smoothed Relative Strength Index over 14 periods. "
                        + "Values above 70 indicate overbought; below 30 oversold.",
            Parameters:   new Dictionary<string, object> { ["period"] = 14 },
            FeatureIndex: 0,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(0, 100),
            Unit:         "",
            DefaultColor: "#f0883e"),

        new(
            Id:           "macd_signal",
            Name:         "MACD Signal (12/26/9)",
            Description:  "EMA(9) of the MACD line (EMA12 − EMA26), normalised by "
                        + "close price. Positive = bullish momentum; negative = bearish.",
            Parameters:   new Dictionary<string, object> { ["fast"] = 12, ["slow"] = 26, ["signal"] = 9 },
            FeatureIndex: 1,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Histogram,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "",
            DefaultColor: "#00d4ff"),

        new(
            Id:           "bb_position",
            Name:         "Bollinger Band Position (20, 2)",
            Description:  "(close − lower_band) / (upper_band − lower_band) using "
                        + "BB(20, 2 std-dev). 0 = lower band; 1 = upper band; outside [0,1] means band breach.",
            Parameters:   new Dictionary<string, object> { ["period"] = 20, ["stddev"] = 2 },
            FeatureIndex: 2,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(0, 1),
            Unit:         "",
            DefaultColor: "#bc8cff"),

        new(
            Id:           "volume_delta",
            Name:         "Volume Delta",
            Description:  "(vol[n] − vol[n−1]) / vol[n−1]. Relative volume change "
                        + "vs the previous bar; positive = rising volume, negative = shrinking.",
            Parameters:   new Dictionary<string, object>(),
            FeatureIndex: 3,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Histogram,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "%",
            DefaultColor: "#2ea043"),

        new(
            Id:           "momentum_20",
            Name:         "Momentum (20)",
            Description:  "close[n] / close[n−20] − 1. Twenty-bar price return as "
                        + "a decimal fraction; positive = upward drift, negative = downward.",
            Parameters:   new Dictionary<string, object> { ["period"] = 20 },
            FeatureIndex: 4,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "%",
            DefaultColor: "#ffab00"),

        new(
            Id:           "atr_norm",
            Name:         "ATR Normalised (14)",
            Description:  "Wilder ATR(14) divided by close price. Scale-free "
                        + "volatility measure; higher values indicate wider candle ranges.",
            Parameters:   new Dictionary<string, object> { ["period"] = 14 },
            FeatureIndex: 5,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(0, null),
            Unit:         "",
            DefaultColor: "#f85149"),

        new(
            Id:           "spread_bps",
            Name:         "Spread (bps)",
            Description:  "(high − low) / close * 10 000. Candle high-low range "
                        + "expressed in basis points; proxy for intra-bar liquidity cost.",
            Parameters:   new Dictionary<string, object>(),
            FeatureIndex: 6,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Histogram,
            ValueRange:   new IndicatorValueRange(0, null),
            Unit:         "bps",
            DefaultColor: "#8b949e"),

        new(
            Id:           "vwap_distance",
            Name:         "VWAP Distance",
            Description:  "(close − VWAP) / VWAP where VWAP = sum(close * vol) / sum(vol) "
                        + "over the computation window. Positive = close above VWAP.",
            Parameters:   new Dictionary<string, object>(),
            FeatureIndex: 7,
            ModelTypes:   [ModelType.Trading],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "%",
            DefaultColor: "#79c0ff"),

        // ── Library-only indicators (future model expansion / custom blocks) ──

        new(
            Id:           "sma_20",
            Name:         "SMA (20)",
            Description:  "Simple Moving Average over 20 periods. Price overlay; "
                        + "useful as a dynamic support/resistance level.",
            Parameters:   new Dictionary<string, object> { ["period"] = 20 },
            FeatureIndex: -1,
            ModelTypes:   [],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "",
            DefaultColor: "#ffab00"),

        new(
            Id:           "ema_9",
            Name:         "EMA (9)",
            Description:  "Exponential Moving Average over 9 periods. Faster "
                        + "reaction to recent price changes than SMA.",
            Parameters:   new Dictionary<string, object> { ["period"] = 9 },
            FeatureIndex: -1,
            ModelTypes:   [],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "",
            DefaultColor: "#00d4ff"),

        new(
            Id:           "stoch_rsi_14",
            Name:         "StochRSI (14)",
            Description:  "Stochastic of RSI: (RSI − min(RSI,n)) / (max(RSI,n) − min(RSI,n)). "
                        + "Range [0, 1]; higher sensitivity than plain RSI for short-term signals.",
            Parameters:   new Dictionary<string, object> { ["period"] = 14 },
            FeatureIndex: -1,
            ModelTypes:   [],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(0, 1),
            Unit:         "",
            DefaultColor: "#bc8cff"),

        new(
            Id:           "cci_20",
            Name:         "CCI (20)",
            Description:  "Commodity Channel Index: (typical_price − SMA20) / (0.015 * mean_deviation). "
                        + "Oscillates around zero; ±100 thresholds signal overbought/oversold.",
            Parameters:   new Dictionary<string, object> { ["period"] = 20 },
            FeatureIndex: -1,
            ModelTypes:   [],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(-200, 200),
            Unit:         "",
            DefaultColor: "#f0883e"),

        new(
            Id:           "obv",
            Name:         "OBV",
            Description:  "On-Balance Volume: cumulative sum of ±volume based on "
                        + "close direction vs prior close. Divergence from price signals trend weakness.",
            Parameters:   new Dictionary<string, object>(),
            FeatureIndex: -1,
            ModelTypes:   [],
            PlotType:     IndicatorPlotType.Area,
            ValueRange:   new IndicatorValueRange(null, null),
            Unit:         "",
            DefaultColor: "#2ea043"),

        new(
            Id:           "williams_r_14",
            Name:         "Williams %R (14)",
            Description:  "(highest_high − close) / (highest_high − lowest_low) * -100. "
                        + "Range [−100, 0]; above −20 overbought, below −80 oversold.",
            Parameters:   new Dictionary<string, object> { ["period"] = 14 },
            FeatureIndex: -1,
            ModelTypes:   [],
            PlotType:     IndicatorPlotType.Line,
            ValueRange:   new IndicatorValueRange(-100, 0),
            Unit:         "",
            DefaultColor: "#79c0ff"),
    ];

    // ── Lookup tables (built once at static init) ─────────────────────────────

    private static readonly Dictionary<string, IndicatorDescriptor> _byId =
        _all.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<int, IndicatorDescriptor> _byFeatureIndex =
        _all.Where(d => d.FeatureIndex >= 0)
            .ToDictionary(d => d.FeatureIndex);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the complete indicator catalog (model-wired + library-only).
    /// </summary>
    public static IReadOnlyList<IndicatorDescriptor> GetAll() => _all;

    /// <summary>
    /// Returns all indicators whose feature vector includes the given
    /// <paramref name="modelType"/>, ordered by <see cref="IndicatorDescriptor.FeatureIndex"/>.
    /// </summary>
    public static IReadOnlyList<IndicatorDescriptor> GetByModelType(ModelType modelType) =>
        [.. _all.Where(d => d.ModelTypes.Contains(modelType))
                .OrderBy(d => d.FeatureIndex)];

    /// <summary>
    /// Looks up an indicator by its unique <paramref name="id"/> (case-insensitive).
    /// Returns <see langword="true"/> and populates <paramref name="descriptor"/> on success.
    /// </summary>
    public static bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IndicatorDescriptor? descriptor) =>
        _byId.TryGetValue(id, out descriptor);

    /// <summary>
    /// Returns the descriptor for the indicator occupying the given
    /// <paramref name="featureIndex"/> slot in the model feature vector,
    /// or <see langword="null"/> if no registered indicator occupies that slot.
    /// </summary>
    public static IndicatorDescriptor? GetByFeatureIndex(int featureIndex) =>
        _byFeatureIndex.GetValueOrDefault(featureIndex);
}
