using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Average True Range indicator block.
/// Measures market volatility. Emits ATR value on <see cref="BlockSocketType.IndicatorValue"/>.
/// </summary>
public sealed class ATRBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input", BlockSocketType.CandleStream),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue),
    ];

    private float _atr      = float.NaN;
    private float _prevClose = float.NaN;
    private int   _count;

    private readonly BlockParameter<int> _periodParam =
        new("Period", "Period", "ATR smoothing period", 14, MinValue: 2, MaxValue: 200, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "ATRBlock";
    /// <inheritdoc/>
    public override string DisplayName => "ATR";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_periodParam];

    /// <summary>Initialises a new <see cref="ATRBlock"/>.</summary>
    public ATRBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _atr       = float.NaN;
        _prevClose = float.NaN;
        _count     = 0;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractOHLC(signal.Value, out var high, out var low, out var close))
            return new ValueTask<BlockSignal?>(result: null);

        var period = _periodParam.DefaultValue;
        var tr = float.IsNaN(_prevClose)
            ? high - low
            : MathF.Max(high - low, MathF.Max(MathF.Abs(high - _prevClose), MathF.Abs(low - _prevClose)));

        _prevClose = close;
        _count++;

        if (float.IsNaN(_atr))
        {
            _atr = tr;
            if (_count < period)
                return new ValueTask<BlockSignal?>(result: null);
        }
        else
        {
            // Wilder's smoothing
            _atr = (_atr * (period - 1) + tr) / period;
        }

        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "indicator_output", BlockSocketType.IndicatorValue, _atr));
    }

    private static bool TryExtractOHLC(JsonElement value, out float high, out float low, out float close)
    {
        high  = float.NaN;
        low   = float.NaN;
        close = float.NaN;

        if (value.ValueKind != JsonValueKind.Object)
            return false;

        if (!value.TryGetProperty("high",  out var h) || !h.TryGetSingle(out high))  return false;
        if (!value.TryGetProperty("low",   out var l) || !l.TryGetSingle(out low))   return false;
        if (!value.TryGetProperty("close", out var c) || !c.TryGetSingle(out close)) return false;

        return true;
    }
}
