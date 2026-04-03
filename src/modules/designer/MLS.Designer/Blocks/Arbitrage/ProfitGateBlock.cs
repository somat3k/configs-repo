using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Arbitrage;

/// <summary>
/// Profit gate block — pass-through filter that only emits an arbitrage opportunity
/// when the estimated net profit exceeds the configured threshold (spread minus gas cost).
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.ArbitrageOpportunity"/>. <br/>
/// Output: <see cref="BlockSocketType.ArbitrageOpportunity"/> (pass-through when profitable). <br/>
/// An opportunity is passed only when <c>net_profit ≥ MinProfitUsd</c>.
/// </remarks>
public sealed class ProfitGateBlock : BlockBase
{
    private readonly BlockParameter<decimal> _minProfitUsdParam =
        new("MinProfitUsd", "Min Net Profit (USD)", "Minimum net profit in USD (after gas) to pass", 10m,
            MinValue: 0.01m, MaxValue: 100_000m, IsOptimizable: true);
    private readonly BlockParameter<decimal> _gasEstimateUsdParam =
        new("GasEstimateUsd", "Gas Estimate (USD)", "Estimated gas cost per execution in USD", 2m,
            MinValue: 0m, MaxValue: 100m, IsOptimizable: false);

    /// <inheritdoc/>
    public override string BlockType   => "ProfitGateBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Profit Gate";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_minProfitUsdParam, _gasEstimateUsdParam];

    /// <summary>Initialises a new <see cref="ProfitGateBlock"/>.</summary>
    public ProfitGateBlock() : base(
        [BlockSocket.Input("arb_opportunity", BlockSocketType.ArbitrageOpportunity)],
        [BlockSocket.Output("gated_opportunity", BlockSocketType.ArbitrageOpportunity)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.ArbitrageOpportunity)
            return new ValueTask<BlockSignal?>(result: null);

        decimal spreadUsd    = 0m;
        decimal gasOverride  = 0m;
        decimal netProfit    = 0m;
        bool    hasNetProfit = false;

        if (signal.Value.ValueKind == JsonValueKind.Object)
        {
            if (signal.Value.TryGetProperty("net_profit", out var np) && np.TryGetDecimal(out var npDec))
            {
                netProfit    = npDec;
                hasNetProfit = true;
            }

            if (signal.Value.TryGetProperty("spread", out var sp) && sp.TryGetSingle(out var spf))
                spreadUsd = (decimal)spf;

            if (signal.Value.TryGetProperty("gas_usd", out var gu) && gu.TryGetDecimal(out var guDec))
                gasOverride = guDec;
        }

        // Prefer explicit net_profit; otherwise compute from spread − gas
        var effectiveGas    = gasOverride > 0 ? gasOverride : _gasEstimateUsdParam.DefaultValue;
        var effectiveProfit = hasNetProfit ? netProfit : spreadUsd - effectiveGas;

        if (effectiveProfit < _minProfitUsdParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        // Pass through with gas annotation
        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "gated_opportunity", BlockSocketType.ArbitrageOpportunity, signal.Value));
    }
}
