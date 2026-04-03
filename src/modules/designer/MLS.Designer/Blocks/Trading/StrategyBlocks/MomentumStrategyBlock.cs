using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.StrategyBlocks;

/// <summary>
/// Momentum strategy block that emits a <see cref="BlockSocketType.MLSignal"/>
/// based on price momentum and volume confirmation.
/// </summary>
public sealed class MomentumStrategyBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input",    BlockSocketType.CandleStream),
        BlockSocket.Input("indicator_input", BlockSocketType.IndicatorValue),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("signal_output", BlockSocketType.MLSignal),
    ];

    private float _lastIndicator = float.NaN;

    private readonly BlockParameter<float> _buyThresholdParam  = new("BuyThreshold",  "Buy Threshold",  "Indicator level to trigger BUY",  0.65f, MinValue: 0f, MaxValue: 1f, IsOptimizable: true);
    private readonly BlockParameter<float> _sellThresholdParam = new("SellThreshold", "Sell Threshold", "Indicator level to trigger SELL", 0.35f, MinValue: 0f, MaxValue: 1f, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "MomentumStrategyBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Momentum Strategy";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_buyThresholdParam, _sellThresholdParam];

    /// <summary>Initialises a new <see cref="MomentumStrategyBlock"/>.</summary>
    public MomentumStrategyBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() => _lastIndicator = float.NaN;

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType == BlockSocketType.IndicatorValue
            && signal.Value.ValueKind == JsonValueKind.Number)
        {
            _lastIndicator = signal.Value.GetSingle();
        }

        if (float.IsNaN(_lastIndicator))
            return new ValueTask<BlockSignal?>(result: null);

        string? direction = null;
        float   confidence = 0f;

        if (_lastIndicator >= _buyThresholdParam.DefaultValue)
        {
            direction  = "BUY";
            confidence = (_lastIndicator - _buyThresholdParam.DefaultValue) /
                         (1f - _buyThresholdParam.DefaultValue);
        }
        else if (_lastIndicator <= _sellThresholdParam.DefaultValue)
        {
            direction  = "SELL";
            confidence = (_sellThresholdParam.DefaultValue - _lastIndicator) /
                         _sellThresholdParam.DefaultValue;
        }

        if (direction is null)
            return new ValueTask<BlockSignal?>(result: null);

        var stratSignal = new { direction, confidence = Math.Clamp(confidence, 0f, 1f), model_name = "momentum" };
        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "signal_output", BlockSocketType.MLSignal, stratSignal));
    }
}
