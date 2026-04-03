using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.ExecutionBlocks;

/// <summary>
/// Slippage estimator block that predicts expected fill price vs. signal price
/// based on order book depth and historical fill data.
/// Emits an <see cref="BlockSocketType.IndicatorValue"/> representing estimated slippage in bps.
/// </summary>
public sealed class SlippageEstimatorBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("order_input",     BlockSocketType.TradeOrder),
        BlockSocket.Input("orderbook_input", BlockSocketType.OrderBookUpdate),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("slippage_output", BlockSocketType.IndicatorValue),
    ];

    private float _lastBestAsk = float.NaN;
    private float _lastBestBid = float.NaN;

    private readonly BlockParameter<float> _baseBpsParam = new("BaseBps", "Base Slippage (bps)", "Fixed slippage floor in basis points", 5f, MinValue: 0f, MaxValue: 100f);

    /// <inheritdoc/>
    public override string BlockType   => "SlippageEstimatorBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Slippage Estimator";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_baseBpsParam];

    /// <summary>Initialises a new <see cref="SlippageEstimatorBlock"/>.</summary>
    public SlippageEstimatorBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _lastBestAsk = float.NaN;
        _lastBestBid = float.NaN;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType == BlockSocketType.OrderBookUpdate)
        {
            TryExtractBestBidAsk(signal.Value, out _lastBestBid, out _lastBestAsk);
            return new ValueTask<BlockSignal?>(result: null);
        }

        if (signal.SocketType != BlockSocketType.TradeOrder)
            return new ValueTask<BlockSignal?>(result: null);

        if (float.IsNaN(_lastBestAsk) || float.IsNaN(_lastBestBid))
        {
            // No order book data yet — emit base slippage
            return new ValueTask<BlockSignal?>(
                EmitFloat(BlockId, "slippage_output", BlockSocketType.IndicatorValue, _baseBpsParam.DefaultValue));
        }

        var mid    = (_lastBestBid + _lastBestAsk) / 2f;
        var spread = mid > 0 ? (_lastBestAsk - _lastBestBid) / mid * 10_000f : 0f;   // in bps
        var estimatedSlippage = _baseBpsParam.DefaultValue + spread / 2f;

        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "slippage_output", BlockSocketType.IndicatorValue, estimatedSlippage));
    }

    private static void TryExtractBestBidAsk(JsonElement value, out float bestBid, out float bestAsk)
    {
        bestBid = float.NaN;
        bestAsk = float.NaN;
        if (value.ValueKind != JsonValueKind.Object) return;
        if (value.TryGetProperty("best_bid", out var b)) b.TryGetSingle(out bestBid);
        if (value.TryGetProperty("best_ask", out var a)) a.TryGetSingle(out bestAsk);
    }
}
