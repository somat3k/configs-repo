using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DataHydra;

/// <summary>
/// Feed source block — subscribes to a live exchange feed (OHLCV candles) from the
/// Data Layer module and emits each received candle on the <c>candle_output</c> socket.
/// </summary>
/// <remarks>
/// <para>
/// Connection: the block sends a <c>DATA_COLLECTION_START</c> envelope to the Block Controller
/// requesting the Data Layer to begin (or continue) collecting candles for the configured
/// exchange / symbol / timeframe.  Candles arrive as <c>CANDLE_STREAM</c> envelopes forwarded
/// by the Block Controller.
/// </para>
/// <para>
/// Output: <see cref="BlockSocketType.CandleStream"/> carrying a structured OHLCV candle payload.
/// </para>
/// </remarks>
public sealed class FeedSourceBlock : BlockBase
{
    private readonly BlockParameter<string> _exchangeParam =
        new("Exchange",   "Exchange",   "Exchange identifier (e.g. hyperliquid, camelot)",  "hyperliquid");
    private readonly BlockParameter<string> _symbolParam =
        new("Symbol",     "Symbol",     "Trading pair symbol (e.g. BTC-USDT)",               "BTC-USDT");
    private readonly BlockParameter<string> _timeframeParam =
        new("Timeframe",  "Timeframe",  "Candle timeframe (e.g. 1m, 5m, 1h, 1d)",            "1m");

    /// <inheritdoc/>
    public override string BlockType   => "FeedSourceBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Feed Source";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_exchangeParam, _symbolParam, _timeframeParam];

    /// <summary>Initialises a <see cref="FeedSourceBlock"/>.</summary>
    public FeedSourceBlock() : base(
        [],
        [BlockSocket.Output("candle_output", BlockSocketType.CandleStream)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // FeedSourceBlock is a data source — it is activated by external candle envelopes routed
        // from the Block Controller, not by processing upstream block signals.
        // When a CandleStream envelope arrives (forwarded by the graph engine), emit it downstream.
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        var exchange = _exchangeParam.DefaultValue;
        var symbol   = _symbolParam.DefaultValue;

        // Filter: only pass candles matching our configured exchange/symbol
        if (signal.Value.ValueKind == JsonValueKind.Object)
        {
            if (signal.Value.TryGetProperty("exchange", out var exEl) &&
                !string.Equals(exEl.GetString(), exchange, StringComparison.OrdinalIgnoreCase))
                return new ValueTask<BlockSignal?>(result: null);

            if (signal.Value.TryGetProperty("symbol", out var symEl) &&
                !string.Equals(symEl.GetString(), symbol, StringComparison.OrdinalIgnoreCase))
                return new ValueTask<BlockSignal?>(result: null);
        }

        return new ValueTask<BlockSignal?>(
            new BlockSignal(BlockId, "candle_output", BlockSocketType.CandleStream, signal.Value));
    }
}
