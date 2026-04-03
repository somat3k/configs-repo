using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Morpho borrow block — emits a <see cref="BlockSocketType.DeFiSignal"/> to borrow an asset
/// against collateral on Morpho Blue.
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.DeFiSignal"/> (supply confirmation from <see cref="MorphoSupplyBlock"/>). <br/>
/// Output: <see cref="BlockSocketType.DeFiSignal"/> with <c>protocol = "morpho"</c> and
///         <c>action = "borrow"</c>.
/// </remarks>
public sealed class MorphoBorrowBlock : BlockBase
{
    private readonly BlockParameter<string> _borrowAssetParam =
        new("BorrowAsset", "Borrow Asset", "ERC-20 asset to borrow (e.g. 'WETH')", "WETH");
    private readonly BlockParameter<decimal> _borrowAmountParam =
        new("BorrowAmount", "Borrow Amount", "Amount to borrow in token units", 0.5m,
            MinValue: 0.001m, MaxValue: 10_000m, IsOptimizable: false);
    private readonly BlockParameter<decimal> _maxLtvParam =
        new("MaxLtv", "Max LTV (%)", "Maximum loan-to-value ratio to maintain (e.g. 75)", 75m,
            MinValue: 10m, MaxValue: 95m, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "MorphoBorrowBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Morpho Borrow";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_borrowAssetParam, _borrowAmountParam, _maxLtvParam];

    /// <summary>Initialises a new <see cref="MorphoBorrowBlock"/>.</summary>
    public MorphoBorrowBlock() : base(
        [BlockSocket.Input("supply_signal", BlockSocketType.DeFiSignal)],
        [BlockSocket.Output("defi_signal", BlockSocketType.DeFiSignal)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.DeFiSignal)
            return new ValueTask<BlockSignal?>(result: null);

        // Only act on supply confirmations
        if (signal.Value.ValueKind == JsonValueKind.Object
            && signal.Value.TryGetProperty("action", out var actionEl)
            && !string.Equals(actionEl.GetString(), "supply", StringComparison.OrdinalIgnoreCase))
        {
            return new ValueTask<BlockSignal?>(result: null);
        }

        var borrowSignal = new
        {
            protocol     = "morpho",
            action       = "borrow",
            asset        = _borrowAssetParam.DefaultValue,
            amount       = _borrowAmountParam.DefaultValue,
            max_ltv      = _maxLtvParam.DefaultValue,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "defi_signal", BlockSocketType.DeFiSignal, borrowSignal));
    }
}
