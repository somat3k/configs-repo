using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Arbitrage;

/// <summary>
/// Spread calculator block — computes the price spread between two configured exchanges.
/// Receives <see cref="BlockSocketType.OnChainEvent"/> signals carrying
/// <c>{ exchange, symbol, price }</c> price-update payloads.
/// Emits an <see cref="BlockSocketType.ArbitrageOpportunity"/> signal when the spread
/// between the two configured exchanges exceeds the minimum threshold.
/// </summary>
/// <remarks>
/// Spread = price_exchange_a − price_exchange_b.
/// </remarks>
public sealed class SpreadCalculatorBlock : BlockBase
{
    // Latest observed price per exchange key
    private readonly Dictionary<string, float> _prices = new(StringComparer.OrdinalIgnoreCase);

    private readonly BlockParameter<string> _exchangeAParam =
        new("ExchangeA", "Exchange A", "Source exchange for price A (e.g. 'camelot')", "camelot");
    private readonly BlockParameter<string> _exchangeBParam =
        new("ExchangeB", "Exchange B", "Source exchange for price B (e.g. 'dfyn')", "dfyn");
    private readonly BlockParameter<string> _symbolParam =
        new("Symbol", "Symbol", "Token pair to compare (e.g. 'WETH/USDC')", "WETH/USDC");
    private readonly BlockParameter<float> _minSpreadBpsParam =
        new("MinSpreadBps", "Min Spread (bps)", "Minimum spread in basis points to emit an opportunity", 5f,
            MinValue: 0f, MaxValue: 1000f, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "SpreadCalculatorBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Spread Calculator";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_exchangeAParam, _exchangeBParam, _symbolParam, _minSpreadBpsParam];

    /// <summary>Initialises a new <see cref="SpreadCalculatorBlock"/>.</summary>
    public SpreadCalculatorBlock() : base(
        [BlockSocket.Input("price_update", BlockSocketType.OnChainEvent)],
        [BlockSocket.Output("arb_opportunity", BlockSocketType.ArbitrageOpportunity)]) { }

    /// <inheritdoc/>
    public override void Reset() => _prices.Clear();

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.OnChainEvent)
            return new ValueTask<BlockSignal?>(result: null);

        // Expect payload: { exchange: "camelot", symbol: "WETH/USDC", price: 2000.5 }
        if (!TryExtractPriceUpdate(signal.Value, out var exchange, out var symbol, out var price))
            return new ValueTask<BlockSignal?>(result: null);

        // Filter to configured symbol
        if (!string.IsNullOrEmpty(symbol)
            && !symbol.Equals(_symbolParam.DefaultValue, StringComparison.OrdinalIgnoreCase))
            return new ValueTask<BlockSignal?>(result: null);

        _prices[exchange] = price;

        var exA = _exchangeAParam.DefaultValue;
        var exB = _exchangeBParam.DefaultValue;

        if (!_prices.TryGetValue(exA, out var priceA) ||
            !_prices.TryGetValue(exB, out var priceB))
            return new ValueTask<BlockSignal?>(result: null);

        var spread    = priceA - priceB;
        var spreadBps = Math.Abs(priceB) > 1e-10f
            ? Math.Abs(spread) / priceB * 10_000f
            : 0f;

        if (spreadBps < _minSpreadBpsParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        var opportunity = new
        {
            symbol        = _symbolParam.DefaultValue,
            exchange_a    = exA,
            exchange_b    = exB,
            price_a       = priceA,
            price_b       = priceB,
            spread        = spread,
            spread_bps    = spreadBps,
            buy_exchange  = spread > 0 ? exB : exA,
            sell_exchange = spread > 0 ? exA : exB,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "arb_opportunity", BlockSocketType.ArbitrageOpportunity, opportunity));
    }

    private static bool TryExtractPriceUpdate(
        JsonElement value, out string exchange, out string symbol, out float price)
    {
        exchange = string.Empty;
        symbol   = string.Empty;
        price    = float.NaN;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("exchange", out var exEl))
            exchange = exEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("symbol", out var symEl))
            symbol = symEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("price", out var priceEl))
        {
            if (priceEl.ValueKind == JsonValueKind.Number)
                price = priceEl.GetSingle();
            else if (priceEl.ValueKind == JsonValueKind.String
                     && float.TryParse(priceEl.GetString(), out var pf))
                price = pf;
        }

        return !string.IsNullOrEmpty(exchange) && !float.IsNaN(price);
    }
}
