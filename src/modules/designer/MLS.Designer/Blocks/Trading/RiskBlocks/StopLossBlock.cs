using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.RiskBlocks;

/// <summary>
/// Stop-loss block that tracks open positions and emits a SELL <see cref="BlockSocketType.RiskDecision"/>
/// when the loss threshold is breached.
/// </summary>
public sealed class StopLossBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("candle_input",    BlockSocketType.CandleStream),
        BlockSocket.Input("indicator_input", BlockSocketType.IndicatorValue),   // ATR for dynamic stops
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("risk_output", BlockSocketType.RiskDecision),
    ];

    private float _entryPrice = float.NaN;
    private float _lastAtr    = float.NaN;

    private readonly BlockParameter<float>  _fixedStopPctParam = new("FixedStopPct", "Fixed Stop %",  "Hard stop as % below entry",     1f,  MinValue: 0.1f, MaxValue: 20f, IsOptimizable: true);
    private readonly BlockParameter<float>  _atrMultParam      = new("AtrMultiplier","ATR Multiplier","ATR-based stop multiplier",       2f,  MinValue: 0.5f, MaxValue: 10f, IsOptimizable: true);
    private readonly BlockParameter<bool>   _trailingParam     = new("Trailing",      "Trailing",      "Enable trailing stop-loss",       true);
    private float                           _trailingHighest;

    /// <inheritdoc/>
    public override string BlockType   => "StopLossBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Stop Loss";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_fixedStopPctParam, _atrMultParam, _trailingParam];

    /// <summary>Initialises a new <see cref="StopLossBlock"/>.</summary>
    public StopLossBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _entryPrice      = float.NaN;
        _lastAtr         = float.NaN;
        _trailingHighest = float.NaN;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType == BlockSocketType.IndicatorValue
            && signal.Value.ValueKind == JsonValueKind.Number)
        {
            _lastAtr = signal.Value.GetSingle();
            return new ValueTask<BlockSignal?>(result: null);
        }

        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractClose(signal.Value, out var close))
            return new ValueTask<BlockSignal?>(result: null);

        if (float.IsNaN(_entryPrice))
        {
            _entryPrice      = close;
            _trailingHighest = close;
            return new ValueTask<BlockSignal?>(result: null);
        }

        if (_trailingParam.DefaultValue)
            _trailingHighest = MathF.Max(_trailingHighest, close);

        var stopPrice = ComputeStop(_trailingParam.DefaultValue ? _trailingHighest : _entryPrice);

        if (close <= stopPrice)
        {
            _entryPrice = float.NaN;    // Reset position
            var d = new { allow = false, direction = "SELL", reason = "stop_loss", stop_price = stopPrice };
            return new ValueTask<BlockSignal?>(EmitObject(BlockId, "risk_output", BlockSocketType.RiskDecision, d));
        }

        return new ValueTask<BlockSignal?>(result: null);
    }

    private float ComputeStop(float referencePrice)
    {
        if (!float.IsNaN(_lastAtr) && _lastAtr > 0)
            return referencePrice - _atrMultParam.DefaultValue * _lastAtr;
        return referencePrice * (1f - _fixedStopPctParam.DefaultValue / 100f);
    }

    private static bool TryExtractClose(JsonElement value, out float close)
    {
        close = float.NaN;
        if (value.ValueKind == JsonValueKind.Number) { close = value.GetSingle(); return true; }
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("close", out var p) && p.TryGetSingle(out close)) return true;
        return false;
    }
}
