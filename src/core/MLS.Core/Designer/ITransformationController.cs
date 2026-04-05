namespace MLS.Core.Designer;

/// <summary>
/// Central coordinator responsible for routing <see cref="TransformationEnvelope"/> objects
/// through the block graph, dispatching to the correct named sub-division, and collecting results.
/// </summary>
/// <remarks>
/// The Transformation Controller (TC) is a framework-level component providing:
/// <list type="bullet">
///   <item>Unified payload routing through named sub-divisions (e.g. <c>"risk"</c>, <c>"ml"</c>, <c>"defi"</c>).</item>
///   <item>Ordered processing pipeline — blocks within a sub-division are processed in registration order.</item>
///   <item>Full audit trail — every transformation step is logged with block ID, type, and timestamp.</item>
/// </list>
/// </remarks>
public interface ITransformationController
{
    /// <summary>
    /// Route an envelope through the specified sub-division.
    /// Returns the transformed envelope after all blocks in that sub-division have processed the signal.
    /// If no blocks are registered for the sub-division, the envelope is returned unchanged.
    /// </summary>
    /// <param name="envelope">The envelope to route.</param>
    /// <param name="subDivision">
    /// Named sub-division to dispatch to.  Use constants from <see cref="MLS.Core.Constants.SubDivision"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<TransformationEnvelope> RouteAsync(
        TransformationEnvelope envelope,
        string subDivision,
        CancellationToken ct);

    /// <summary>
    /// Register a block as a participant in a named sub-division.
    /// Blocks are processed in registration order within each sub-division.
    /// </summary>
    /// <param name="subDivision">Target sub-division name.</param>
    /// <param name="block">Block to register as a processing step.</param>
    void RegisterBlock(string subDivision, IBlockElement block);

    /// <summary>
    /// Retrieve the transformation history accumulated for a specific origin signal.
    /// The key is the <see cref="BlockSignal.SourceBlockId"/> of the originating block.
    /// Returns an empty list when no history has been recorded for the given ID.
    /// </summary>
    /// <param name="originSignalId">
    /// The source block ID used as the audit key (matches <c>TransformationHistory[0].BlockId</c>).
    /// </param>
    IReadOnlyList<TransformationUnit> GetHistory(Guid originSignalId);
}
