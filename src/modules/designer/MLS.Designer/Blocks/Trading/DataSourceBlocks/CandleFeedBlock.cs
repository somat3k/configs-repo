using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.DataSourceBlocks;

/// <summary>
/// Data source block that subscribes to the DataLayer CANDLE_STREAM feed
/// and emits <see cref="BlockSocketType.CandleStream"/> signals to downstream blocks.
/// </summary>
public sealed class CandleFeedBlock : BlockBase
{
    // ── Sockets ───────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyList<IBlockSocket> _inputs  = [];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("candle_output", BlockSocketType.CandleStream),
    ];

    // ── Parameters ────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyList<BlockParameter> _parameters =
    [
        new BlockParameter<string>("Symbol",    "Symbol",    "Trading pair, e.g. BTC-PERP",   "BTC-PERP"),
        new BlockParameter<string>("Timeframe", "Timeframe", "Candle interval: 1m 5m 15m 1h", "5m"),
        new BlockParameter<string>("Exchange",  "Exchange",  "Exchange source",                 "hyperliquid"),
    ];

    // ── IBlockElement ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override string BlockType    => "CandleFeedBlock";
    /// <inheritdoc/>
    public override string DisplayName  => "Candle Feed";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => _parameters;

    /// <summary>Initialises a new <see cref="CandleFeedBlock"/>.</summary>
    public CandleFeedBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // CandleFeedBlock is a data source: it passes through incoming CANDLE_STREAM signals
        if (signal.SocketType == BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "candle_output", BlockSocketType.CandleStream, signal.Value));

        return new ValueTask<BlockSignal?>(result: null);
    }
}
