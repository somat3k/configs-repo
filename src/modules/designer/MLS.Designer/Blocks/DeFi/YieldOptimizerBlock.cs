using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Yield optimizer block — automatically rebalances assets across DeFi protocols
/// to maximise yield. Compares APY across Morpho, Balancer, and emits a rebalance signal
/// when a better rate is found.
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.HealthFactorUpdate"/> (position health stream). <br/>
/// Output: <see cref="BlockSocketType.DeFiSignal"/> with <c>action = "rebalance"</c>
///         pointing to the optimal protocol.
/// </remarks>
public sealed class YieldOptimizerBlock : BlockBase
{
    // Track latest APY per protocol
    private readonly Dictionary<string, decimal> _apyByProtocol = new(StringComparer.OrdinalIgnoreCase);

    private readonly BlockParameter<decimal> _minApyImprovementParam =
        new("MinApyImprovement", "Min APY Improvement (%)", "Only rebalance if improvement exceeds this percentage", 0.5m,
            MinValue: 0.01m, MaxValue: 20m, IsOptimizable: true);
    private readonly BlockParameter<string> _assetParam =
        new("Asset", "Asset", "Asset to optimise yield for (e.g. 'USDC')", "USDC");
    private readonly BlockParameter<decimal> _amountParam =
        new("Amount", "Amount", "Amount to rebalance in token units", 1000m,
            MinValue: 1m, MaxValue: 10_000_000m, IsOptimizable: false);

    /// <inheritdoc/>
    public override string BlockType   => "YieldOptimizerBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Yield Optimizer";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_minApyImprovementParam, _assetParam, _amountParam];

    /// <summary>Initialises a new <see cref="YieldOptimizerBlock"/>.</summary>
    public YieldOptimizerBlock() : base(
        [BlockSocket.Input("health_update", BlockSocketType.HealthFactorUpdate)],
        [BlockSocket.Output("defi_signal", BlockSocketType.DeFiSignal)]) { }

    /// <inheritdoc/>
    public override void Reset() => _apyByProtocol.Clear();

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.HealthFactorUpdate)
            return new ValueTask<BlockSignal?>(result: null);

        // Extract APY update for a protocol from the health update payload
        if (!TryExtractApyUpdate(signal.Value, out var protocol, out var apy))
            return new ValueTask<BlockSignal?>(result: null);

        _apyByProtocol[protocol] = apy;

        if (_apyByProtocol.Count < 2)
            return new ValueTask<BlockSignal?>(result: null);

        // Find current best
        var (bestProtocol, bestApy) = _apyByProtocol.MaxBy(kv => kv.Value);

        // Find current protocol (highest existing that is NOT best)
        var current = _apyByProtocol
            .Where(kv => !kv.Key.Equals(bestProtocol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Value)
            .Select(kv => (Protocol: kv.Key, Apy: kv.Value))
            .FirstOrDefault();

        if (current.Protocol is null) return new ValueTask<BlockSignal?>(result: null);

        var improvement = bestApy - current.Apy;
        if (improvement < _minApyImprovementParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        var rebalanceSignal = new
        {
            protocol      = bestProtocol,
            action        = "rebalance",
            asset         = _assetParam.DefaultValue,
            amount        = _amountParam.DefaultValue,
            from_protocol = current.Protocol,
            current_apy   = current.Apy,
            target_apy    = bestApy,
            improvement   = improvement,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "defi_signal", BlockSocketType.DeFiSignal, rebalanceSignal));
    }

    private static bool TryExtractApyUpdate(JsonElement value, out string protocol, out decimal apy)
    {
        protocol = string.Empty;
        apy      = 0m;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("protocol", out var pEl))
            protocol = pEl.GetString() ?? string.Empty;

        if (value.TryGetProperty("supply_apy", out var apyEl) && apyEl.TryGetDecimal(out apy))
            return !string.IsNullOrEmpty(protocol);

        if (value.TryGetProperty("apy", out var apy2El) && apy2El.TryGetDecimal(out apy))
            return !string.IsNullOrEmpty(protocol);

        return false;
    }
}
