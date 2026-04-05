namespace MLS.Core.Designer;

/// <summary>
/// Unified envelope wrapper that carries the original <see cref="BlockSignal"/> together
/// with the ordered list of <see cref="TransformationUnit"/> records accumulated during
/// graph traversal.
/// </summary>
/// <remarks>
/// <para>Envelopes are <b>immutable</b>; each transformation step produces a new instance
/// via <see cref="WithTransformation"/>.</para>
/// <para>The first element of <see cref="TransformationHistory"/> is always the origin block;
/// the last element is the most recently applied transformation.</para>
/// </remarks>
/// <param name="Signal">The signal payload at its current (most recently transformed) state.</param>
/// <param name="TransformationHistory">
/// Ordered list of transformations applied since the signal originated.
/// </param>
public sealed record TransformationEnvelope(
    BlockSignal Signal,
    IReadOnlyList<TransformationUnit> TransformationHistory)
{
    /// <summary>Create a new envelope with a single origin transformation unit.</summary>
    public static TransformationEnvelope Create(BlockSignal signal, TransformationUnit origin) =>
        new(signal, [origin]);

    /// <summary>
    /// Append a new transformation unit and return a new envelope.
    /// Produces a new instance with the updated history (immutable append).
    /// </summary>
    public TransformationEnvelope WithTransformation(TransformationUnit unit) =>
        this with { TransformationHistory = [..TransformationHistory, unit] };

    /// <summary>
    /// Origin block ID — shortcut to <c>TransformationHistory[0].BlockId</c>.
    /// Returns <see cref="Guid.Empty"/> when the history is empty.
    /// </summary>
    public Guid OriginBlockId =>
        TransformationHistory.Count > 0 ? TransformationHistory[0].BlockId : Guid.Empty;
}
