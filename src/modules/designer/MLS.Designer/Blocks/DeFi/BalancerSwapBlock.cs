using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Balancer swap block — emits a <see cref="BlockSocketType.DeFiSignal"/> to execute an AMM swap
/// through a Balancer weighted pool.
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.ArbitrageOpportunity"/> or
///         <see cref="BlockSocketType.DeFiSignal"/> (rebalance trigger). <br/>
/// Output: <see cref="BlockSocketType.DeFiSignal"/> with <c>protocol = "balancer"</c> and
///         <c>action = "swap"</c>.
/// </remarks>
public sealed class BalancerSwapBlock : BlockBase
{
    private readonly BlockParameter<string> _tokenInParam =
        new("TokenIn", "Token In", "Input token symbol (e.g. 'WETH')", "WETH");
    private readonly BlockParameter<string> _tokenOutParam =
        new("TokenOut", "Token Out", "Output token symbol (e.g. 'USDC')", "USDC");
    private readonly BlockParameter<decimal> _amountInParam =
        new("AmountIn", "Amount In", "Swap amount in TokenIn units", 1m,
            MinValue: 0.0001m, MaxValue: 100_000m, IsOptimizable: false);
    private readonly BlockParameter<decimal> _slippagePctParam =
        new("SlippagePct", "Slippage %", "Maximum acceptable slippage in percent", 0.5m,
            MinValue: 0.01m, MaxValue: 5m, IsOptimizable: false);

    /// <inheritdoc/>
    public override string BlockType   => "BalancerSwapBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Balancer Swap";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_tokenInParam, _tokenOutParam, _amountInParam, _slippagePctParam];

    /// <summary>Initialises a new <see cref="BalancerSwapBlock"/>.</summary>
    public BalancerSwapBlock() : base(
        [
            BlockSocket.Input("arb_trigger",  BlockSocketType.ArbitrageOpportunity),
            BlockSocket.Input("defi_trigger", BlockSocketType.DeFiSignal),
        ],
        [BlockSocket.Output("defi_signal", BlockSocketType.DeFiSignal)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.ArbitrageOpportunity
         && signal.SocketType != BlockSocketType.DeFiSignal)
        {
            return new ValueTask<BlockSignal?>(result: null);
        }

        // Extract token overrides from the incoming signal if available
        var tokenIn  = _tokenInParam.DefaultValue;
        var tokenOut = _tokenOutParam.DefaultValue;
        var amount   = _amountInParam.DefaultValue;

        if (signal.Value.ValueKind == JsonValueKind.Object)
        {
            if (signal.Value.TryGetProperty("token_in", out var ti))
                tokenIn = ti.GetString() ?? tokenIn;
            if (signal.Value.TryGetProperty("token_out", out var to))
                tokenOut = to.GetString() ?? tokenOut;
            if (signal.Value.TryGetProperty("amount", out var am) && am.TryGetDecimal(out var amDec))
                amount = amDec;
        }

        var swapSignal = new
        {
            protocol    = "balancer",
            action      = "swap",
            token_in    = tokenIn,
            token_out   = tokenOut,
            amount_in   = amount,
            slippage_pct = _slippagePctParam.DefaultValue,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "defi_signal", BlockSocketType.DeFiSignal, swapSignal));
    }
}
