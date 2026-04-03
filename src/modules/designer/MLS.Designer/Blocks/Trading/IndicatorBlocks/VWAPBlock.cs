using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.IndicatorBlocks;

/// <summary>
/// Volume-Weighted Average Price indicator block.
/// Computes VWAP as <c>cumulative(price × volume) / cumulative(volume)</c>.
/// Emits the normalised deviation from VWAP on <see cref="BlockSocketType.IndicatorValue"/>.
/// </summary>
public sealed class VWAPBlock : BlockBase
{
    private double _cumulativePV;
    private double _cumulativeV;
    private DateOnly _lastDate;

    private readonly BlockParameter<bool> _resetDailyParam =
        new("ResetDaily", "Reset Daily", "Reset VWAP at start of each day", true);

    /// <inheritdoc/>
    public override string BlockType   => "VWAPBlock";
    /// <inheritdoc/>
    public override string DisplayName => "VWAP";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_resetDailyParam];

    /// <summary>Initialises a new <see cref="VWAPBlock"/>.</summary>
    public VWAPBlock() : base(
        [BlockSocket.Input("candle_input", BlockSocketType.CandleStream)],
        [BlockSocket.Output("indicator_output", BlockSocketType.IndicatorValue)]) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _cumulativePV = 0;
        _cumulativeV  = 0;
        _lastDate     = default;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractTypicalPriceAndVolume(signal.Value, out var typicalPrice, out var volume))
            return new ValueTask<BlockSignal?>(result: null);

        // Daily reset
        if (_resetDailyParam.DefaultValue)
        {
            var signalDate = DateOnly.FromDateTime(signal.Timestamp.UtcDateTime);
            if (_lastDate != default && signalDate != _lastDate)
            {
                Reset();           // clears _cumulativePV, _cumulativeV, _lastDate
                _lastDate = signalDate; // re-set date after reset so next tick doesn't re-trigger
            }
            else
            {
                _lastDate = signalDate;
            }
        }

        _cumulativePV += typicalPrice * volume;
        _cumulativeV  += volume;

        if (_cumulativeV < 1e-12)
            return new ValueTask<BlockSignal?>(result: null);

        var vwap = _cumulativePV / _cumulativeV;
        // Normalise: deviation from VWAP as a fraction of VWAP
        var deviation = (float)((typicalPrice - vwap) / vwap);

        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, "indicator_output", BlockSocketType.IndicatorValue, deviation));
    }

    private static bool TryExtractTypicalPriceAndVolume(JsonElement value, out double typicalPrice, out double volume)
    {
        typicalPrice = 0;
        volume       = 0;

        if (value.ValueKind != JsonValueKind.Object)
            return false;

        if (!value.TryGetProperty("high",   out var h) || !h.TryGetDouble(out var high))   return false;
        if (!value.TryGetProperty("low",    out var l) || !l.TryGetDouble(out var low))    return false;
        if (!value.TryGetProperty("close",  out var c) || !c.TryGetDouble(out var close))  return false;
        if (!value.TryGetProperty("volume", out var v) || !v.TryGetDouble(out volume))     return false;

        typicalPrice = (high + low + close) / 3.0;
        return true;
    }
}
