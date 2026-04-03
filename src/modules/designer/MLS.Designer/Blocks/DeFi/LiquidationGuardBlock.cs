using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Liquidation guard block — emergency close trigger when a position's health factor
/// falls below the configured liquidation threshold.
/// Emits a <see cref="BlockSocketType.DeFiSignal"/> with <c>action = "emergency_close"</c>.
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.HealthFactorUpdate"/> (streaming HF monitor output). <br/>
/// Output: <see cref="BlockSocketType.DeFiSignal"/> with <c>action = "emergency_close"</c>
///         when HF &lt; liquidation threshold. <br/>
/// Once triggered, the guard stays active until <see cref="Reset"/> is called.
/// </remarks>
public sealed class LiquidationGuardBlock : BlockBase
{
    private bool _triggered;

    private readonly BlockParameter<decimal> _liquidationThresholdParam =
        new("LiquidationThreshold", "Liquidation Threshold",
            "Health factor below which emergency close is triggered (e.g. 1.05)", 1.05m,
            MinValue: 1.001m, MaxValue: 1.5m, IsOptimizable: false);
    private readonly BlockParameter<string> _protocolParam =
        new("Protocol", "Protocol", "Protocol to close position on (e.g. 'morpho')", "morpho");
    private readonly BlockParameter<string> _collateralAssetParam =
        new("CollateralAsset", "Collateral Asset", "Collateral asset symbol to withdraw (e.g. 'WETH')", "WETH");
    private readonly BlockParameter<string> _borrowAssetParam =
        new("BorrowAsset", "Borrow Asset", "Borrowed asset symbol to repay (e.g. 'USDC')", "USDC");

    /// <inheritdoc/>
    public override string BlockType   => "LiquidationGuardBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Liquidation Guard";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_liquidationThresholdParam, _protocolParam, _collateralAssetParam, _borrowAssetParam];

    /// <summary>Initialises a new <see cref="LiquidationGuardBlock"/>.</summary>
    public LiquidationGuardBlock() : base(
        [BlockSocket.Input("health_update", BlockSocketType.HealthFactorUpdate)],
        [BlockSocket.Output("defi_signal", BlockSocketType.DeFiSignal)]) { }

    /// <inheritdoc/>
    public override void Reset() => _triggered = false;

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.HealthFactorUpdate)
            return new ValueTask<BlockSignal?>(result: null);

        if (_triggered) return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractHealthFactor(signal.Value, out var hf))
            return new ValueTask<BlockSignal?>(result: null);

        if (hf >= _liquidationThresholdParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        _triggered = true;

        var emergencySignal = new
        {
            protocol          = _protocolParam.DefaultValue,
            action            = "emergency_close",
            collateral_asset  = _collateralAssetParam.DefaultValue,
            borrow_asset      = _borrowAssetParam.DefaultValue,
            health_factor     = hf,
            threshold         = _liquidationThresholdParam.DefaultValue,
            reason            = "health_factor_below_liquidation_threshold",
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "defi_signal", BlockSocketType.DeFiSignal, emergencySignal));
    }

    private static bool TryExtractHealthFactor(JsonElement value, out decimal hf)
    {
        hf = decimal.MaxValue;
        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("health_factor", out var hfEl))
        {
            if (hfEl.TryGetDecimal(out hf)) return true;
            if (hfEl.ValueKind == JsonValueKind.String && decimal.TryParse(hfEl.GetString(), out hf)) return true;
        }
        return false;
    }
}
