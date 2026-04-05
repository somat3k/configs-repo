using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DataHydra;

/// <summary>
/// Router block — routes incoming normalised candles to one or both of two output channels:
/// the chart panel (for live visualisation) and the strategy graph (for signal processing).
/// </summary>
/// <remarks>
/// <para>
/// Input:  <see cref="BlockSocketType.CandleStream"/> (normalised candle). <br/>
/// Output: <see cref="BlockSocketType.CandleStream"/> on <c>chart_output</c> (canvas panel). <br/>
/// Output: <see cref="BlockSocketType.CandleStream"/> on <c>strategy_output</c> (strategy graph).
/// </para>
/// <para>
/// The router runs in three modes controlled by the <c>RouteMode</c> parameter:
/// <list type="bullet">
///   <item><c>both</c> — emit on both outputs (default).</item>
///   <item><c>chart</c> — emit on <c>chart_output</c> only.</item>
///   <item><c>strategy</c> — emit on <c>strategy_output</c> only.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class RouterBlock : BlockBase
{
    private readonly BlockParameter<string> _routeModeParam =
        new("RouteMode", "Route Mode",
            "Routing target: 'both', 'chart', or 'strategy'", "both");

    /// <inheritdoc/>
    public override string BlockType   => "RouterBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Router";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_routeModeParam];

    // Route mode controls which outputs emit — _extraOutput reserved for future per-socket routing
    private readonly string _routeModeNote = "dual-output via direct EmitSignalAsync";

    /// <summary>Initialises a <see cref="RouterBlock"/>.</summary>
    public RouterBlock() : base(
        [BlockSocket.Input("candle_input",    BlockSocketType.CandleStream)],
        [
            BlockSocket.Output("chart_output",    BlockSocketType.CandleStream),
            BlockSocket.Output("strategy_output", BlockSocketType.CandleStream),
        ]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return null;

        var mode = _routeModeParam.DefaultValue;

        if (mode is "chart" or "both")
        {
            var chartSig = new BlockSignal(BlockId, "chart_output", BlockSocketType.CandleStream, signal.Value);
            await EmitSignalAsync(chartSig, ct).ConfigureAwait(false);
        }

        if (mode is "strategy" or "both")
        {
            var stratSig = new BlockSignal(BlockId, "strategy_output", BlockSocketType.CandleStream, signal.Value);
            await EmitSignalAsync(stratSig, ct).ConfigureAwait(false);
        }

        // Return null — we already emitted directly
        return null;
    }
}
