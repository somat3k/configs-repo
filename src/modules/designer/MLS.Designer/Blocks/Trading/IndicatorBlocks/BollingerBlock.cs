using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Bollinger Bands indicator block.
/// Emits the position of close price within the bands as a value in [0, 1]
/// on <see cref="BlockSocketType.IndicatorValue"/>:
/// <c>0</c> = at lower band, <c>0.5</c> = at middle band, <c>1</c> = at upper band.
/// </summary>
public sealed class BollingerBlock : BlockBase
{
    private float[] _window;
    private int     _head;
    private int     _count;
    private float   _sum;

    private readonly BlockParameter<int>   _periodParam =
        new("Period", "Period", "Rolling window length", 20, MinValue: 2, MaxValue: 500, IsOptimizable: true);
    private readonly BlockParameter<float> _multParam =
        new("Multiplier", "Std Dev ×", "Band width multiplier", 2f, MinValue: 0.5f, MaxValue: 5f, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "BollingerBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Bollinger Bands";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_periodParam, _multParam];

    /// <summary>Initialises a new <see cref="BollingerBlock"/>.</summary>
    public BollingerBlock() : base(
        [BlockSocket.Input("candle_input", BlockSocketType.CandleStream)],
        [BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue)])
    {
        _window = new float[_periodParam.DefaultValue];
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        Array.Clear(_window, 0, _window.Length);
        _head  = 0;
        _count = 0;
        _sum   = 0f;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        var close = ExtractClose(signal.Value);
        if (float.IsNaN(close))
            return new ValueTask<BlockSignal?>(result: null);

        var period = _periodParam.DefaultValue;

        // Grow the window array if period changed
        if (_window.Length != period)
        {
            _window = new float[period];
            Reset();
        }

        // Subtract the value being overwritten
        _sum -= _window[_head];
        _window[_head] = close;
        _sum += close;
        _head = (_head + 1) % period;
        if (_count < period) _count++;

        if (_count < period)
            return new ValueTask<BlockSignal?>(result: null);

        var mean = _sum / period;

        // Compute standard deviation (population)
        float variance = 0f;
        for (var i = 0; i < period; i++)
            variance += (_window[i] - mean) * (_window[i] - mean);
        variance /= period;
        var stdDev = MathF.Sqrt(variance);

        var upper = mean + _multParam.DefaultValue * stdDev;
        var lower = mean - _multParam.DefaultValue * stdDev;

        var bandWidth = upper - lower;
        var position  = bandWidth < 1e-10f ? 0.5f : Math.Clamp((close - lower) / bandWidth, 0f, 1f);

        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "indicator_output", BlockSocketType.IndicatorValue, position));
    }

    private static float ExtractClose(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.GetSingle();
        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("close", out var p)
            && p.TryGetSingle(out var f))
            return f;
        return float.NaN;
    }
}
