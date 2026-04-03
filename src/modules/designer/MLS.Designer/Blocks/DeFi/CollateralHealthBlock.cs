using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Collateral health block — monitors a Morpho position's health factor and emits
/// streaming <see cref="BlockSocketType.HealthFactorUpdate"/> signals.
/// Triggers a <c>DEFI_HEALTH_WARNING</c> when HF falls below the configured alert threshold.
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.DeFiSignal"/> (position state updates). <br/>
/// Output: <see cref="BlockSocketType.HealthFactorUpdate"/> (streaming HF with severity). <br/>
/// </remarks>
public sealed class CollateralHealthBlock : BlockBase
{
    private decimal _currentHf = decimal.MaxValue;

    private readonly BlockParameter<decimal> _alertThresholdParam =
        new("AlertThreshold", "Alert Threshold", "Health factor below which a WARNING is emitted", 1.5m,
            MinValue: 1.01m, MaxValue: 5m, IsOptimizable: false);
    private readonly BlockParameter<decimal> _criticalThresholdParam =
        new("CriticalThreshold", "Critical Threshold", "Health factor below which a CRITICAL alert is emitted", 1.15m,
            MinValue: 1.01m, MaxValue: 3m, IsOptimizable: false);
    private readonly BlockParameter<string> _protocolParam =
        new("Protocol", "Protocol", "Lending protocol to monitor (e.g. 'morpho')", "morpho");
    private readonly BlockParameter<string> _collateralAssetParam =
        new("CollateralAsset", "Collateral Asset", "Collateral asset symbol (e.g. 'WETH')", "WETH");
    private readonly BlockParameter<string> _borrowAssetParam =
        new("BorrowAsset", "Borrow Asset", "Borrowed asset symbol (e.g. 'USDC')", "USDC");

    /// <inheritdoc/>
    public override string BlockType   => "CollateralHealthBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Collateral Health";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_alertThresholdParam, _criticalThresholdParam, _protocolParam, _collateralAssetParam, _borrowAssetParam];

    /// <summary>Initialises a new <see cref="CollateralHealthBlock"/>.</summary>
    public CollateralHealthBlock() : base(
        [BlockSocket.Input("defi_signal", BlockSocketType.DeFiSignal)],
        [BlockSocket.Output("health_update", BlockSocketType.HealthFactorUpdate)]) { }

    /// <inheritdoc/>
    public override void Reset() => _currentHf = decimal.MaxValue;

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.DeFiSignal)
            return new ValueTask<BlockSignal?>(result: null);

        // Extract health factor from the DeFi signal payload
        if (!TryExtractHealthFactor(signal.Value, out var hf))
            return new ValueTask<BlockSignal?>(result: null);

        _currentHf = hf;

        var severity = hf < _criticalThresholdParam.DefaultValue ? "Critical"
                     : hf < _alertThresholdParam.DefaultValue    ? "Warning"
                     : "Healthy";

        var healthUpdate = new
        {
            protocol            = _protocolParam.DefaultValue,
            collateral_asset    = _collateralAssetParam.DefaultValue,
            borrow_asset        = _borrowAssetParam.DefaultValue,
            health_factor       = hf,
            alert_threshold     = _alertThresholdParam.DefaultValue,
            critical_threshold  = _criticalThresholdParam.DefaultValue,
            severity,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "health_update", BlockSocketType.HealthFactorUpdate, healthUpdate));
    }

    private static bool TryExtractHealthFactor(JsonElement value, out decimal hf)
    {
        hf = 0m;
        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("health_factor", out var hfEl))
        {
            if (hfEl.TryGetDecimal(out hf)) return true;
            if (hfEl.ValueKind == JsonValueKind.String && decimal.TryParse(hfEl.GetString(), out hf)) return true;
        }

        // Fallback: compute from collateral_usd / debt_usd * lltv
        if (value.TryGetProperty("collateral_usd", out var collEl)
            && value.TryGetProperty("debt_usd", out var debtEl)
            && collEl.TryGetDecimal(out var coll)
            && debtEl.TryGetDecimal(out var debt)
            && debt > 0)
        {
            var lltvFactor = 0.86m; // typical Morpho LLTV for WETH/USDC
            hf = coll * lltvFactor / debt;
            return true;
        }

        return false;
    }
}
