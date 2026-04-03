using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Moving Average Convergence Divergence indicator block.
/// Computes MACD line (fast EMA − slow EMA) and signal line (EMA of MACD).
/// Emits <see cref="BlockSocketType.IndicatorValue"/> with the MACD histogram value.
/// </summary>
public sealed class MACDBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input", BlockSocketType.CandleStream),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue),
    ];

    // ── EMA state ─────────────────────────────────────────────────────────────────
    private float _fastEma  = float.NaN;
    private float _slowEma  = float.NaN;
    private float _signalEma = float.NaN;
    private int   _count;

    private readonly BlockParameter<int> _fastParam =
        new("FastPeriod",   "Fast Period",   "Fast EMA period",   12, MinValue: 2, MaxValue: 100, IsOptimizable: true);
    private readonly BlockParameter<int> _slowParam =
        new("SlowPeriod",   "Slow Period",   "Slow EMA period",   26, MinValue: 5, MaxValue: 200, IsOptimizable: true);
    private readonly BlockParameter<int> _signalParam =
        new("SignalPeriod", "Signal Period", "Signal EMA period",  9, MinValue: 2, MaxValue: 50,  IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "MACDBlock";
    /// <inheritdoc/>
    public override string DisplayName => "MACD";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_fastParam, _slowParam, _signalParam];

    /// <summary>Initialises a new <see cref="MACDBlock"/>.</summary>
    public MACDBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _fastEma   = float.NaN;
        _slowEma   = float.NaN;
        _signalEma = float.NaN;
        _count     = 0;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        var close = ExtractClose(signal.Value);
        if (float.IsNaN(close))
            return new ValueTask<BlockSignal?>(result: null);

        var fastK   = 2f / (_fastParam.DefaultValue   + 1);
        var slowK   = 2f / (_slowParam.DefaultValue   + 1);
        var signalK = 2f / (_signalParam.DefaultValue + 1);

        _count++;

        _fastEma  = float.IsNaN(_fastEma)  ? close : close * fastK   + _fastEma  * (1 - fastK);
        _slowEma  = float.IsNaN(_slowEma)  ? close : close * slowK   + _slowEma  * (1 - slowK);

        if (_count < _slowParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        var macd    = _fastEma - _slowEma;
        _signalEma  = float.IsNaN(_signalEma) ? macd : macd * signalK + _signalEma * (1 - signalK);

        if (_count < _slowParam.DefaultValue + _signalParam.DefaultValue - 1)
            return new ValueTask<BlockSignal?>(result: null);

        var histogram = macd - _signalEma;
        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "indicator_output", BlockSocketType.IndicatorValue, histogram));
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
