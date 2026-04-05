namespace MLS.Core.Designer;

/// <summary>
/// Describes a single transformation applied to a <see cref="BlockSignal"/> as it passed
/// through a specific block.  Attached to the <see cref="TransformationEnvelope"/> so
/// downstream consumers know the full processing history of the signal.
/// </summary>
/// <param name="BlockId">The block that applied this transformation.</param>
/// <param name="BlockType">Human-readable type name of the block (e.g. <c>"FeatureEngineerBlock"</c>).</param>
/// <param name="SubDivision">
/// Named sub-division this block belongs to (e.g. <c>"ml"</c>, <c>"risk"</c>, <c>"defi"</c>).
/// Use constants from <see cref="MLS.Core.Constants.SubDivision"/>.
/// </param>
/// <param name="AppliedAt">UTC timestamp when the transformation was applied.</param>
/// <param name="Metadata">
/// Optional key-value metadata describing what was changed.
/// Examples: <c>{ "rows_dropped": 12, "features_added": ["RSI", "MACD"] }</c>.
/// </param>
public sealed record TransformationUnit(
    Guid BlockId,
    string BlockType,
    string SubDivision,
    DateTimeOffset AppliedAt,
    IReadOnlyDictionary<string, object>? Metadata = null);
