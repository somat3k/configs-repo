namespace MLS.DataLayer.FeatureStore;

// ── Plot type ─────────────────────────────────────────────────────────────────

/// <summary>
/// Describes how an indicator series should be rendered on a chart panel.
/// Consumed by <c>canvas-interop.js:initIndicatorChart</c>.
/// </summary>
public enum IndicatorPlotType
{
    /// <summary>
    /// Continuous line series. Typical for oscillators and moving averages that
    /// overlay or share a panel with the price chart.
    /// </summary>
    Line = 0,

    /// <summary>
    /// Vertical bar histogram rendered in a separate panel below the price chart.
    /// Typical for MACD histogram, Volume Delta, and Spread.
    /// </summary>
    Histogram = 1,

    /// <summary>
    /// Area (filled line) series. Typical for cumulative indicators such as OBV.
    /// </summary>
    Area = 2,
}

// ── Value range ───────────────────────────────────────────────────────────────

/// <summary>
/// Suggested y-axis display bounds for chart auto-scaling.
/// A <see langword="null"/> entry means "auto-scale this boundary".
/// </summary>
/// <param name="Min">Lower bound, or <see langword="null"/> for auto.</param>
/// <param name="Max">Upper bound, or <see langword="null"/> for auto.</param>
public sealed record IndicatorValueRange(double? Min, double? Max);

// ── Descriptor ────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable descriptor for a single technical indicator registered in
/// <see cref="IndicatorLibrary"/>.
/// </summary>
/// <remarks>
/// <para>
/// Descriptors carry two kinds of metadata:
/// <list type="bullet">
///   <item><b>Computation metadata</b> — <see cref="FeatureIndex"/>,
///     <see cref="ModelTypes"/>, <see cref="Parameters"/>.</item>
///   <item><b>Chart/plot metadata</b> — <see cref="PlotType"/>,
///     <see cref="ValueRange"/>, <see cref="Unit"/>, <see cref="DefaultColor"/>.</item>
/// </list>
/// </para>
/// <para>
/// The chart metadata is designed to be serialised and passed directly to
/// <c>canvas-interop.js:initIndicatorChart</c> so that the web-app can build
/// ApexCharts instances without any hard-coded knowledge of indicator configuration.
/// </para>
/// </remarks>
/// <param name="Id">
/// Unique slug used as the <c>seriesName</c> argument in <c>updateApexSeries</c>
/// (e.g. <c>"rsi_14"</c>). Case-insensitive across the library.
/// </param>
/// <param name="Name">Human-readable display name, e.g. <c>"RSI (14)"</c>.</param>
/// <param name="Description">
/// One-sentence explanation of what the indicator measures and its typical use.
/// </param>
/// <param name="Parameters">
/// Key → value map of computation parameters (period, multiplier, etc.).
/// Empty for parameterless indicators such as Volume Delta.
/// </param>
/// <param name="FeatureIndex">
/// Zero-based index into <see cref="FeatureVector.ToArray()"/> for indicators
/// wired into a model feature vector, or <c>-1</c> for library-only indicators
/// not yet assigned to a model.
/// </param>
/// <param name="ModelTypes">
/// Model types whose feature vector includes this indicator at
/// <see cref="FeatureIndex"/>. Empty for library-only indicators.
/// </param>
/// <param name="PlotType">How to render this indicator series on a chart.</param>
/// <param name="ValueRange">
/// Suggested y-axis bounds; <see langword="null"/> min/max entries mean auto-scale.
/// </param>
/// <param name="Unit">
/// Display unit appended to tooltip values (e.g. <c>"bps"</c>, <c>"%"</c>, or
/// <c>""</c> for dimensionless values).
/// </param>
/// <param name="DefaultColor">
/// Default hex colour string for the chart series (e.g. <c>"#00d4ff"</c>).
/// </param>
public sealed record IndicatorDescriptor(
    string                              Id,
    string                              Name,
    string                              Description,
    IReadOnlyDictionary<string, object> Parameters,
    int                                 FeatureIndex,
    IReadOnlyList<ModelType>            ModelTypes,
    IndicatorPlotType                   PlotType,
    IndicatorValueRange                 ValueRange,
    string                              Unit,
    string                              DefaultColor);
