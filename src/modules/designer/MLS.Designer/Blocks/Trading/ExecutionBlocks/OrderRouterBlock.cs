using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.ExecutionBlocks;

/// <summary>
/// Smart order router that selects the best execution venue (HYPERLIQUID or Camelot AMM)
/// based on order type and size. Passes <see cref="BlockSocketType.TradeOrder"/> to the
/// optimal destination broker topic.
/// </summary>
public sealed class OrderRouterBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("order_input", BlockSocketType.TradeOrder),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("order_output", BlockSocketType.TradeOrder),
    ];

    private readonly BlockParameter<float>  _camelotThresholdParam = new("CamelotThresholdUsd", "Camelot Threshold (USD)", "Route to Camelot AMM if size < this threshold", 5000f, MinValue: 100f, MaxValue: 500_000f);
    private readonly BlockParameter<string> _defaultExchangeParam  = new("DefaultExchange",      "Default Exchange",       "Default exchange for perp trades",               "hyperliquid");

    /// <inheritdoc/>
    public override string BlockType   => "OrderRouterBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Order Router";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_camelotThresholdParam, _defaultExchangeParam];

    /// <summary>Initialises a new <see cref="OrderRouterBlock"/>.</summary>
    public OrderRouterBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.TradeOrder)
            return new ValueTask<BlockSignal?>(result: null);

        var sizeUsd = ExtractSizeUsd(signal.Value);
        var route   = sizeUsd > 0 && sizeUsd < _camelotThresholdParam.DefaultValue
            ? "camelot"
            : _defaultExchangeParam.DefaultValue;

        // Attach routing metadata and re-emit
        var routedOrder = new
        {
            original       = signal.Value,
            exchange_route = route,
            routed_at      = DateTimeOffset.UtcNow,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "order_output", BlockSocketType.TradeOrder, routedOrder));
    }

    private static float ExtractSizeUsd(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("size_usd", out var s) && s.TryGetSingle(out var v))
            return v;
        return 0f;
    }
}
