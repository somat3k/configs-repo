using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.StrategyBlocks;

/// <summary>
/// Mean-reversion strategy block using Z-score of an indicator.
/// Emits BUY when Z-score &lt; −threshold, SELL when Z-score &gt; threshold.
/// </summary>
public sealed class MeanReversionBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("indicator_input", BlockSocketType.IndicatorValue),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("signal_output", BlockSocketType.MLSignal),
    ];

    private float[]  _window;
    private int      _head;
    private int      _count;
    private float    _sum;
    private float    _sumSq;

    private readonly BlockParameter<int>   _lookbackParam  = new("Lookback",  "Lookback",  "Z-score rolling window",   20, MinValue: 5, MaxValue: 500, IsOptimizable: true);
    private readonly BlockParameter<float> _thresholdParam = new("Threshold", "Threshold", "Z-score entry threshold",  2f, MinValue: 0.5f, MaxValue: 5f, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "MeanReversionBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Mean Reversion";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_lookbackParam, _thresholdParam];

    /// <summary>Initialises a new <see cref="MeanReversionBlock"/>.</summary>
    public MeanReversionBlock() : base(_inputs, _outputs)
    {
        _window = new float[_lookbackParam.DefaultValue];
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        Array.Clear(_window, 0, _window.Length);
        _head  = 0;
        _count = 0;
        _sum   = 0f;
        _sumSq = 0f;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.IndicatorValue
            || signal.Value.ValueKind != JsonValueKind.Number)
            return new ValueTask<BlockSignal?>(result: null);

        var value = signal.Value.GetSingle();

        _sum   -= _window[_head];
        _sumSq -= _window[_head] * _window[_head];
        _window[_head] = value;
        _sum   += value;
        _sumSq += value * value;
        _head   = (_head + 1) % _window.Length;
        if (_count < _window.Length) _count++;

        if (_count < _window.Length)
            return new ValueTask<BlockSignal?>(result: null);

        var mean     = _sum / _count;
        var variance = _sumSq / _count - mean * mean;
        var stdDev   = MathF.Sqrt(Math.Max(variance, 0f));

        if (stdDev < 1e-7f)
            return new ValueTask<BlockSignal?>(result: null);

        var zScore = (value - mean) / stdDev;
        var threshold = _thresholdParam.DefaultValue;

        if (zScore > threshold)
        {
            var s = new { direction = "SELL", confidence = Math.Clamp((zScore - threshold) / threshold, 0f, 1f), model_name = "mean_reversion" };
            return new ValueTask<BlockSignal?>(EmitObject(BlockId, "signal_output", BlockSocketType.MLSignal, s));
        }

        if (zScore < -threshold)
        {
            var s = new { direction = "BUY", confidence = Math.Clamp((-zScore - threshold) / threshold, 0f, 1f), model_name = "mean_reversion" };
            return new ValueTask<BlockSignal?>(EmitObject(BlockId, "signal_output", BlockSocketType.MLSignal, s));
        }

        return new ValueTask<BlockSignal?>(result: null);
    }
}
