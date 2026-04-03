using System.Runtime.CompilerServices;
using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Relative Strength Index indicator block.
/// Computes Wilder's smoothed RSI from a <see cref="BlockSocketType.CandleStream"/>
/// and emits a normalised value in [0, 1] on <see cref="BlockSocketType.IndicatorValue"/>.
/// </summary>
/// <remarks>
/// Algorithm: Wilder's smoothed RSI (equivalent to Python ta-lib RSI).
/// Returns <c>null</c> until exactly <c>Period</c> candles have been processed (warm-up phase).
/// </remarks>
public sealed class RSIBlock : BlockBase
{
    private float _prevClose = float.NaN;
    private float _avgGain;
    private float _avgLoss;
    private int   _count;
    private bool  _warmedUp;

    // ── Parameters ────────────────────────────────────────────────────────────────
    private readonly BlockParameter<int> _periodParam =
        new("Period", "Period", "RSI lookback period (Wilder's smoothing)", 14, MinValue: 2, MaxValue: 200, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "RSIBlock";
    /// <inheritdoc/>
    public override string DisplayName => "RSI";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_periodParam];

    /// <summary>Initialises a new <see cref="RSIBlock"/> with default period 14.</summary>
    public RSIBlock() : base(
        [BlockSocket.Input("candle_input", BlockSocketType.CandleStream)],
        [BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue)]) { }

    /// <summary>Initialises with a specific period (for testing).</summary>
    internal RSIBlock(int period) : base(
        [BlockSocket.Input("candle_input",    BlockSocketType.CandleStream)],
        [BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue)])
    {
        _periodParam = new BlockParameter<int>("Period", "Period", "RSI lookback period", period, MinValue: 2, MaxValue: 200, IsOptimizable: true);
    }

    // ── IBlockElement ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void Reset()
    {
        _prevClose = float.NaN;
        _avgGain   = 0f;
        _avgLoss   = 0f;
        _count     = 0;
        _warmedUp  = false;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        var close = ExtractClose(signal.Value);
        if (float.IsNaN(close))
            return new ValueTask<BlockSignal?>(result: null);

        var rsi = UpdateRsi(close, _periodParam.DefaultValue);
        if (!rsi.HasValue)
            return new ValueTask<BlockSignal?>(result: null);

        // Normalise RSI to [0, 1]
        var normalised = rsi.Value / 100f;
        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "indicator_output", BlockSocketType.IndicatorValue, normalised));
    }

    // ── RSI core algorithm (Wilder's smoothing) ───────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float? UpdateRsi(float close, int period)
    {
        if (float.IsNaN(_prevClose))
        {
            _prevClose = close;
            return null;
        }

        var delta = close - _prevClose;
        _prevClose = close;

        var gain = delta > 0f ? delta : 0f;
        var loss = delta < 0f ? -delta : 0f;

        _count++;

        if (_count <= period)
        {
            // Initial accumulation phase
            _avgGain += gain;
            _avgLoss += loss;

            if (_count < period)
                return null;

            // First average (simple mean)
            _avgGain /= period;
            _avgLoss /= period;
            _warmedUp = true;
        }
        else
        {
            // Wilder's smoothing
            _avgGain = (_avgGain * (period - 1) + gain) / period;
            _avgLoss = (_avgLoss * (period - 1) + loss) / period;
        }

        if (!_warmedUp)
            return null;

        if (_avgLoss < 1e-10f)
            return 100f;

        var rs = _avgGain / _avgLoss;
        return 100f - 100f / (1f + rs);
    }

    // ── Candle extraction ─────────────────────────────────────────────────────────

    /// <summary>
    /// Extract the close price from a candle signal value.
    /// Supports <c>{ "close": 42000.5 }</c> JSON object or a raw float.
    /// </summary>
    private static float ExtractClose(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.GetSingle();

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("close", out var closeProp)
            && closeProp.TryGetSingle(out var closeFloat))
            return closeFloat;

        return float.NaN;
    }
}
