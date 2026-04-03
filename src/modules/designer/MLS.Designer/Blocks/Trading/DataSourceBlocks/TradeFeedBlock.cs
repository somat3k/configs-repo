using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.DataSourceBlocks;

/// <summary>
/// Data source block that streams tick-by-tick trade events
/// as <see cref="BlockSocketType.TradeTickStream"/> signals.
/// </summary>
public sealed class TradeFeedBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs  = [];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("trade_output", BlockSocketType.TradeTickStream),
    ];

    private static readonly IReadOnlyList<BlockParameter> _parameters =
    [
        new BlockParameter<string>("Symbol",   "Symbol",   "Trading pair",   "BTC-PERP"),
        new BlockParameter<string>("Exchange", "Exchange", "Exchange source", "hyperliquid"),
    ];

    /// <inheritdoc/>
    public override string BlockType   => "TradeFeedBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Trade Feed";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => _parameters;

    /// <summary>Initialises a new <see cref="TradeFeedBlock"/>.</summary>
    public TradeFeedBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType == BlockSocketType.TradeTickStream)
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "trade_output", BlockSocketType.TradeTickStream, signal.Value));

        return new ValueTask<BlockSignal?>(result: null);
    }
}
