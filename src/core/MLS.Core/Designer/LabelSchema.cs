namespace MLS.Core.Designer;

/// <summary>
/// Describes the shape and per-dimension semantics of a multi-dimensional label tensor
/// produced by <c>TrainSplitBlock</c> for use in multi-task learning scenarios.
/// </summary>
/// <remarks>
/// <para>
/// The standard 1-D label vector (<c>y shape = (n_samples,)</c>) is represented as
/// <c>NDims=1</c> with a single <see cref="LabelDimensionType.ClassIndex"/> dimension.
/// </para>
/// <para>
/// For arbitrage navigation, a 3-D schema encodes direction, magnitude, and confidence
/// simultaneously, enabling multi-task loss functions in the training pipeline.
/// </para>
/// </remarks>
/// <param name="NDims">Number of label dimensions (columns in the label tensor).</param>
/// <param name="DimensionNames">
/// Per-dimension name identifiers (e.g. <c>["direction", "magnitude", "confidence"]</c>).
/// Length must equal <paramref name="NDims"/>.
/// </param>
/// <param name="DimensionTypes">
/// Classification of each label dimension.  Length must equal <paramref name="NDims"/>.
/// </param>
/// <param name="Ranges">
/// Expected value range for each dimension (<c>Min</c>, <c>Max</c>).
/// Length must equal <paramref name="NDims"/>.
/// </param>
public sealed record LabelSchema(
    int NDims,
    string[] DimensionNames,
    LabelDimensionType[] DimensionTypes,
    (double Min, double Max)[] Ranges)
{
    /// <summary>
    /// Standard scalar classification schema: 1-D integer class index.
    /// Compatible with <c>CrossEntropyLoss</c>.
    /// </summary>
    public static readonly LabelSchema Scalar = new(
        NDims: 1,
        DimensionNames: ["class"],
        DimensionTypes: [LabelDimensionType.ClassIndex],
        Ranges: [(0, 2)]);

    /// <summary>
    /// 3-D arbitrage navigation schema: direction class + magnitude + confidence.
    /// Maps to <c>MultiTaskLoss(CrossEntropy, HuberLoss, BCEWithLogitsLoss)</c>.
    /// </summary>
    public static readonly LabelSchema ArbitrageNavigation = new(
        NDims: 3,
        DimensionNames: ["direction", "magnitude", "confidence"],
        DimensionTypes: [LabelDimensionType.ClassIndex, LabelDimensionType.Continuous, LabelDimensionType.Probability],
        Ranges: [(0, 2), (0.0, 1.0), (0.0, 1.0)]);
}

/// <summary>Classification of each label dimension in a <see cref="LabelSchema"/>.</summary>
public enum LabelDimensionType
{
    /// <summary>Integer class index compatible with <c>CrossEntropyLoss</c>.</summary>
    ClassIndex,

    /// <summary>Continuous value in [Min, Max] compatible with <c>MSELoss</c> or <c>HuberLoss</c>.</summary>
    Continuous,

    /// <summary>Probability value in [0, 1] compatible with <c>BinaryCrossEntropyWithLogitsLoss</c>.</summary>
    Probability,
}
