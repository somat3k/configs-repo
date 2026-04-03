using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Morpho supply block — emits a <see cref="BlockSocketType.DeFiSignal"/> to lend an asset
/// at the optimal Morpho Blue market rate.
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.IndicatorValue"/> (trigger — e.g. yield signal or timer). <br/>
/// Output: <see cref="BlockSocketType.DeFiSignal"/> with <c>protocol = "morpho"</c> and
///         <c>action = "supply"</c>.
/// </remarks>
public sealed class MorphoSupplyBlock : BlockBase
{
    private readonly BlockParameter<string> _assetParam =
        new("Asset", "Asset", "ERC-20 asset to supply (e.g. 'USDC')", "USDC");
    private readonly BlockParameter<decimal> _amountParam =
        new("Amount", "Amount", "Amount to supply in token units", 1000m,
            MinValue: 0.001m, MaxValue: 1_000_000m, IsOptimizable: false);
    private readonly BlockParameter<decimal> _minApyParam =
        new("MinApy", "Min APY (%)", "Only supply if market APY exceeds this minimum", 3m,
            MinValue: 0m, MaxValue: 100m, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "MorphoSupplyBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Morpho Supply";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_assetParam, _amountParam, _minApyParam];

    /// <summary>Initialises a new <see cref="MorphoSupplyBlock"/>.</summary>
    public MorphoSupplyBlock() : base(
        [BlockSocket.Input("trigger", BlockSocketType.IndicatorValue)],
        [BlockSocket.Output("defi_signal", BlockSocketType.DeFiSignal)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.IndicatorValue)
            return new ValueTask<BlockSignal?>(result: null);

        // Extract APY from trigger if present; otherwise emit unconditionally
        if (signal.Value.ValueKind == JsonValueKind.Object
            && signal.Value.TryGetProperty("apy", out var apyEl)
            && apyEl.TryGetDecimal(out var apy)
            && apy < _minApyParam.DefaultValue)
        {
            return new ValueTask<BlockSignal?>(result: null);
        }

        var defiSignal = new
        {
            protocol = "morpho",
            action   = "supply",
            asset    = _assetParam.DefaultValue,
            amount   = _amountParam.DefaultValue,
            min_apy  = _minApyParam.DefaultValue,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "defi_signal", BlockSocketType.DeFiSignal, defiSignal));
    }
}
