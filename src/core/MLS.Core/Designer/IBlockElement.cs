namespace MLS.Core.Designer;

/// <summary>
/// Root abstraction for every processing node in the MLS block graph.
/// Modelled on StockSharp's <c>BaseIndicator</c> with an async processing lifecycle.
/// </summary>
/// <remarks>
/// Every concrete implementation MUST:
/// <list type="bullet">
///   <item>Be registered with <c>IBlockRegistry</c> using the <see cref="BlockType"/> string as the key.</item>
///   <item>Be <see cref="IAsyncDisposable"/> and release all subscriptions in <c>DisposeAsync</c>.</item>
///   <item>Keep <see cref="ProcessAsync"/> allocation-free on the hot path.</item>
/// </list>
/// </remarks>
public interface IBlockElement : IAsyncDisposable
{
    /// <summary>Unique runtime identifier for this block instance.</summary>
    Guid BlockId { get; }

    /// <summary>
    /// Registry key that identifies the block type, e.g. <c>"RSIBlock"</c>.
    /// MUST match the key used in <c>IBlockRegistry.Register&lt;T&gt;(key)</c>.
    /// </summary>
    string BlockType { get; }

    /// <summary>Human-readable block label shown in the Designer canvas.</summary>
    string DisplayName { get; }

    /// <summary>Ordered list of input sockets that receive data from upstream blocks.</summary>
    IReadOnlyList<IBlockSocket> InputSockets { get; }

    /// <summary>Ordered list of output sockets that emit processed data to downstream blocks.</summary>
    IReadOnlyList<IBlockSocket> OutputSockets { get; }

    /// <summary>Typed configuration parameters exposed to the Designer UI and hyperparameter search.</summary>
    IReadOnlyList<BlockParameter> Parameters { get; }

    /// <summary>
    /// Process an incoming signal on the hot path.
    /// MUST be allocation-free (use <c>ArrayPool</c>, <c>Span</c>, pre-computed state).
    /// </summary>
    /// <param name="signal">The incoming signal with typed value and source metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct);

    /// <summary>
    /// Warm up internal state from historical data before live execution begins.
    /// MUST call <see cref="Reset"/> at the start to clear any previous state.
    /// </summary>
    /// <param name="historicalData">Historical signals in chronological order.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PreloadAsync(IEnumerable<BlockSignal> historicalData, CancellationToken ct);

    /// <summary>
    /// Clear all internal rolling-window state (arrays, running sums, counters).
    /// Equivalent to <c>BaseIndicator.Reset()</c> in StockSharp.
    /// </summary>
    void Reset();
}
