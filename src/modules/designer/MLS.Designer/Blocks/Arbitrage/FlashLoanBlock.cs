using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Arbitrage;

/// <summary>
/// Flash loan block — emits a <see cref="BlockSocketType.DeFiSignal"/> that initiates
/// an Aave-compatible flash loan to fund a detected arbitrage opportunity.
/// </summary>
/// <remarks>
/// Input: <see cref="BlockSocketType.ArbitrageOpportunity"/> (path to fund). <br/>
/// Output: <see cref="BlockSocketType.DeFiSignal"/> with action = <c>"flash_loan_initiate"</c>. <br/>
/// <para>
/// The flash loan is funded at the configured <see cref="FlashLoanProviderParam"/> address
/// (resolved at runtime from <c>IBlockchainAddressBook</c>).
/// The block emits the initiation signal only — execution is handled by the Transactions module.
/// </para>
/// </remarks>
public sealed class FlashLoanBlock : BlockBase
{
    private readonly BlockParameter<string> _assetParam =
        new("Asset", "Loan Asset", "ERC-20 token to borrow (e.g. 'WETH')", "WETH");
    private readonly BlockParameter<decimal> _loanAmountParam =
        new("LoanAmount", "Loan Amount", "Amount to borrow in token units", 1m,
            MinValue: 0.001m, MaxValue: 1_000m, IsOptimizable: false);
    private readonly BlockParameter<string> _flashLoanProviderParam =
        new("FlashLoanProvider", "Provider", "Flash loan provider key (resolves via address book)", "FlashLoanProvider");
    private readonly BlockParameter<decimal> _maxFeePctParam =
        new("MaxFeePct", "Max Fee %", "Maximum tolerated flash loan fee percentage", 0.09m,
            MinValue: 0.01m, MaxValue: 1m, IsOptimizable: false);

    /// <inheritdoc/>
    public string FlashLoanProviderParam => _flashLoanProviderParam.DefaultValue;

    /// <inheritdoc/>
    public override string BlockType   => "FlashLoanBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Flash Loan";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_assetParam, _loanAmountParam, _flashLoanProviderParam, _maxFeePctParam];

    /// <summary>Initialises a new <see cref="FlashLoanBlock"/>.</summary>
    public FlashLoanBlock() : base(
        [BlockSocket.Input("arb_opportunity", BlockSocketType.ArbitrageOpportunity)],
        [BlockSocket.Output("flash_loan_signal", BlockSocketType.DeFiSignal)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    private static bool TryGetDecimalProperty(JsonElement element, string propertyName, out decimal value)
    {
        value = 0m;

        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetDecimal(out value);
    }

    private static bool TryGetAssetPriceUsd(JsonElement element, out decimal assetPriceUsd)
    {
        assetPriceUsd = 0m;

        return TryGetDecimalProperty(element, "asset_price_usd", out assetPriceUsd)
            || TryGetDecimalProperty(element, "loan_asset_price_usd", out assetPriceUsd)
            || TryGetDecimalProperty(element, "price_usd", out assetPriceUsd);
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.ArbitrageOpportunity)
            return new ValueTask<BlockSignal?>(result: null);

        // `net_profit` is emitted in USD by the upstream arbitrage path finder.
        decimal netProfit = 0m;
        if (TryGetDecimalProperty(signal.Value, "net_profit", out var nDec))
        {
            netProfit = nDec;
        }

        var loanAmount   = _loanAmountParam.DefaultValue;
        var flashFee     = loanAmount * (_maxFeePctParam.DefaultValue / 100m);

        // Convert the flash-loan fee from token units to USD before comparing it to `net_profit`.
        if (!TryGetAssetPriceUsd(signal.Value, out var assetPriceUsd) || assetPriceUsd <= 0m)
            return new ValueTask<BlockSignal?>(result: null);

        var flashFeeUsd = flashFee * assetPriceUsd;

        // Only initiate if net profit in USD covers the flash-loan fee in USD.
        if (netProfit <= flashFeeUsd)
            return new ValueTask<BlockSignal?>(result: null);
        var flashLoanSignal = new
        {
            protocol = _flashLoanProviderParam.DefaultValue,
            action   = "flash_loan_initiate",
            asset    = _assetParam.DefaultValue,
            amount   = loanAmount,
            fee_pct  = _maxFeePctParam.DefaultValue,
            source   = "FlashLoanBlock",
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "flash_loan_signal", BlockSocketType.DeFiSignal, flashLoanSignal));
    }

    private static bool TryGetDecimalProperty(JsonElement element, string key, out decimal value)
    {
        value = 0m;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(key, out var prop)
            && prop.TryGetDecimal(out value);
    }

    private static bool TryGetAssetPriceUsd(JsonElement element, out decimal assetPriceUsd)
    {
        assetPriceUsd = 0m;
        return TryGetDecimalProperty(element, "asset_price_usd",      out assetPriceUsd)
            || TryGetDecimalProperty(element, "loan_asset_price_usd", out assetPriceUsd)
            || TryGetDecimalProperty(element, "price_usd",            out assetPriceUsd);
    }
}
