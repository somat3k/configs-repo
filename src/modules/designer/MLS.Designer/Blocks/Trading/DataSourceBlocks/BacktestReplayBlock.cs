using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.DataSourceBlocks;

/// <summary>
/// Data source block that replays historical OHLCV candles from PostgreSQL
/// for backtesting. Emits <see cref="BlockSocketType.CandleStream"/> signals.
/// </summary>
public sealed class BacktestReplayBlock : BlockBase
{
    private static readonly IReadOnlyList<BlockParameter> _parameters =
    [
        new BlockParameter<string>("Symbol",    "Symbol",     "Trading pair",            "BTC-PERP"),
        new BlockParameter<string>("Timeframe", "Timeframe",  "Candle interval",         "5m"),
        new BlockParameter<string>("Exchange",  "Exchange",   "Exchange source",          "hyperliquid"),
        new BlockParameter<string>("From",      "From (UTC)", "Backtest start (ISO 8601)", "2025-01-01T00:00:00Z"),
        new BlockParameter<string>("To",        "To (UTC)",   "Backtest end (ISO 8601)",   "2026-01-01T00:00:00Z"),
        new BlockParameter<int>   ("SpeedMultiplier", "Speed ×", "Replay speed multiplier", 1, MinValue: 1, MaxValue: 1000),
    ];

    /// <inheritdoc/>
    public override string BlockType   => "BacktestReplayBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Backtest Replay";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => _parameters;

    /// <summary>Initialises a new <see cref="BacktestReplayBlock"/>.</summary>
    public BacktestReplayBlock() : base([], [BlockSocket.Output("candle_output", BlockSocketType.CandleStream)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType == BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "candle_output", BlockSocketType.CandleStream, signal.Value));

        return new ValueTask<BlockSignal?>(result: null);
    }
}
