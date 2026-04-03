using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.StrategyBlocks;

/// <summary>
/// Trend-follow strategy block using moving average crossover.
/// Emits BUY when fast MA crosses above slow MA, SELL on the reverse.
/// </summary>
public sealed class TrendFollowBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input", BlockSocketType.CandleStream),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("signal_output", BlockSocketType.MLSignal),
    ];

    private float _fastEma = float.NaN;
    private float _slowEma = float.NaN;
    private bool  _prevFastAboveSlow;
    private bool  _initialized;

    // Cache EMA smoothing factors to avoid recomputation on every candle
    private readonly float _fastK;
    private readonly float _slowK;

    private readonly BlockParameter<int>   _fastParam = new("FastPeriod", "Fast Period", "Fast EMA period",  9, MinValue: 2, MaxValue: 100, IsOptimizable: true);
    private readonly BlockParameter<int>   _slowParam = new("SlowPeriod", "Slow Period", "Slow EMA period", 21, MinValue: 5, MaxValue: 200, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "TrendFollowBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Trend Follow";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_fastParam, _slowParam];

    /// <summary>Initialises a new <see cref="TrendFollowBlock"/>.</summary>
    public TrendFollowBlock() : base(_inputs, _outputs)
    {
        _fastK = 2f / (_fastParam.DefaultValue + 1);
        _slowK = 2f / (_slowParam.DefaultValue + 1);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _fastEma           = float.NaN;
        _slowEma           = float.NaN;
        _prevFastAboveSlow = false;
        _initialized       = false;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractClose(signal.Value, out var close))
            return new ValueTask<BlockSignal?>(result: null);

        _fastEma = float.IsNaN(_fastEma) ? close : close * _fastK + _fastEma * (1 - _fastK);
        _slowEma = float.IsNaN(_slowEma) ? close : close * _slowK + _slowEma * (1 - _slowK);

        var fastAboveSlow = _fastEma > _slowEma;

        if (!_initialized)
        {
            _prevFastAboveSlow = fastAboveSlow;
            _initialized       = true;
            return new ValueTask<BlockSignal?>(result: null);
        }

        if (fastAboveSlow == _prevFastAboveSlow)
            return new ValueTask<BlockSignal?>(result: null);

        var direction = fastAboveSlow ? "BUY" : "SELL";
        var diff      = MathF.Abs(_fastEma - _slowEma);
        var confidence = Math.Clamp(diff / _slowEma, 0f, 1f);

        _prevFastAboveSlow = fastAboveSlow;

        var s = new { direction, confidence, model_name = "trend_follow" };
        return new ValueTask<BlockSignal?>(EmitObject(BlockId, "signal_output", BlockSocketType.MLSignal, s));
    }

    private static bool TryExtractClose(JsonElement value, out float close)
    {
        close = float.NaN;
        if (value.ValueKind == JsonValueKind.Number) { close = value.GetSingle(); return true; }
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("close", out var p) && p.TryGetSingle(out close)) return true;
        return false;
    }
}
