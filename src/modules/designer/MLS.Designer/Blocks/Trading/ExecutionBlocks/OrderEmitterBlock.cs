using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.ExecutionBlocks;

/// <summary>
/// Order emitter block that converts a <see cref="BlockSocketType.RiskDecision"/> into
/// a <see cref="BlockSocketType.TradeOrder"/> and emits it towards the Broker module.
/// </summary>
public sealed class OrderEmitterBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("risk_input", BlockSocketType.RiskDecision),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("order_output", BlockSocketType.TradeOrder),
    ];

    private readonly BlockParameter<string> _symbolParam   = new("Symbol",   "Symbol",   "Trading symbol",      "BTC-PERP");
    private readonly BlockParameter<string> _orderTypeParam = new("OrderType","Order Type","MARKET or LIMIT",    "MARKET");
    private readonly BlockParameter<float>  _limitOffsetParam = new("LimitOffset", "Limit Offset %", "Limit order offset from mid", 0.1f, MinValue: 0f, MaxValue: 5f);

    /// <inheritdoc/>
    public override string BlockType   => "OrderEmitterBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Order Emitter";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_symbolParam, _orderTypeParam, _limitOffsetParam];

    /// <summary>Initialises a new <see cref="OrderEmitterBlock"/>.</summary>
    public OrderEmitterBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.RiskDecision)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractDecision(signal.Value, out var allow, out var direction, out var sizeUsd))
            return new ValueTask<BlockSignal?>(result: null);

        if (!allow || direction == "HOLD")
            return new ValueTask<BlockSignal?>(result: null);

        var order = new TradeOrderValue(
            OrderId:   Guid.NewGuid().ToString("N"),
            Symbol:    _symbolParam.DefaultValue,
            Side:      direction,
            SizeUsd:   sizeUsd,
            OrderType: _orderTypeParam.DefaultValue,
            CreatedAt: DateTimeOffset.UtcNow);

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "order_output", BlockSocketType.TradeOrder, order));
    }

    private static bool TryExtractDecision(JsonElement value, out bool allow, out string direction, out float sizeUsd)
    {
        allow     = false;
        direction = "HOLD";
        sizeUsd   = 0f;

        if (value.ValueKind != JsonValueKind.Object) return false;
        if (!value.TryGetProperty("allow", out var a)) return false;
        allow = a.GetBoolean();
        if (!value.TryGetProperty("direction", out var d)) return false;
        direction = d.GetString() ?? "HOLD";
        if (value.TryGetProperty("size_usd", out var s)) s.TryGetSingle(out sizeUsd);
        return true;
    }

    private sealed record TradeOrderValue(
        [property: JsonPropertyName("order_id")]   string OrderId,
        [property: JsonPropertyName("symbol")]     string Symbol,
        [property: JsonPropertyName("side")]       string Side,
        [property: JsonPropertyName("size_usd")]   float SizeUsd,
        [property: JsonPropertyName("order_type")] string OrderType,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
