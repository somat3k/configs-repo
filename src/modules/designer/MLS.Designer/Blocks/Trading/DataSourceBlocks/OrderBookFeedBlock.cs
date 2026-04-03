using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.DataSourceBlocks;

/// <summary>
/// Data source block that streams Level-2 order book depth updates
/// as <see cref="BlockSocketType.OrderBookUpdate"/> signals.
/// </summary>
public sealed class OrderBookFeedBlock : BlockBase
{
    private static readonly IReadOnlyList<BlockParameter> _parameters =
    [
        new BlockParameter<string>("Symbol",   "Symbol",   "Trading pair",    "BTC-PERP"),
        new BlockParameter<int>   ("Depth",    "Depth",    "Order book depth", 20, MinValue: 1, MaxValue: 100),
        new BlockParameter<string>("Exchange", "Exchange", "Exchange source",  "hyperliquid"),
    ];

    /// <inheritdoc/>
    public override string BlockType   => "OrderBookFeedBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Order Book Feed";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => _parameters;

    /// <summary>Initialises a new <see cref="OrderBookFeedBlock"/>.</summary>
    public OrderBookFeedBlock() : base([], [BlockSocket.Output("orderbook_output", BlockSocketType.OrderBookUpdate)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType == BlockSocketType.OrderBookUpdate)
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "orderbook_output", BlockSocketType.OrderBookUpdate, signal.Value));

        return new ValueTask<BlockSignal?>(result: null);
    }
}
