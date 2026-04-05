using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DataHydra;

/// <summary>
/// Normalisation block — standardises incoming OHLCV candles to the canonical MLS schema.
/// </summary>
/// <remarks>
/// <para>
/// Normalises OHLCV candles by applying configurable price scaling (e.g. price in native units
/// → USD), aligning field names to the canonical schema, and computing derived fields such as
/// <c>quote_volume = volume × close</c> if not already present.
/// </para>
/// <para>
/// Input:  <see cref="BlockSocketType.CandleStream"/> (raw exchange-specific candle). <br/>
/// Output: <see cref="BlockSocketType.CandleStream"/> (normalised MLS canonical candle).
/// </para>
/// <para>
/// Canonical schema fields:
/// <c>exchange</c>, <c>symbol</c>, <c>timeframe</c>, <c>open_time</c>,
/// <c>open</c>, <c>high</c>, <c>low</c>, <c>close</c>, <c>volume</c>, <c>quote_volume</c>.
/// </para>
/// </remarks>
public sealed class NormalisationBlock : BlockBase
{
    private readonly BlockParameter<decimal> _priceScaleParam =
        new("PriceScale",     "Price Scale",     "Multiply all price fields by this factor (1.0 = no scaling)", 1.0m,
            MinValue: 1e-10m, MaxValue: 1e10m);
    private readonly BlockParameter<decimal> _volumeScaleParam =
        new("VolumeScale",    "Volume Scale",    "Multiply volume fields by this factor (1.0 = no scaling)",    1.0m,
            MinValue: 1e-10m, MaxValue: 1e10m);
    private readonly BlockParameter<bool>    _computeQvParam =
        new("ComputeQuoteVolume", "Compute Quote Volume",
            "If true, compute quote_volume = volume * close when the field is absent", true);

    /// <inheritdoc/>
    public override string BlockType   => "NormalisationBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Normalisation";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_priceScaleParam, _volumeScaleParam, _computeQvParam];

    /// <summary>Initialises a <see cref="NormalisationBlock"/>.</summary>
    public NormalisationBlock() : base(
        [BlockSocket.Input("candle_input",   BlockSocketType.CandleStream)],
        [BlockSocket.Output("candle_output", BlockSocketType.CandleStream)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (signal.Value.ValueKind != JsonValueKind.Object)
            return new ValueTask<BlockSignal?>(result: null);

        var priceScale  = (double)_priceScaleParam.DefaultValue;
        var volScale    = (double)_volumeScaleParam.DefaultValue;
        var computeQv   = _computeQvParam.DefaultValue;

        // Read all canonical fields
        signal.Value.TryGetProperty("exchange",    out var exchange);
        signal.Value.TryGetProperty("symbol",      out var symbol);
        signal.Value.TryGetProperty("timeframe",   out var timeframe);
        signal.Value.TryGetProperty("open_time",   out var openTime);

        double open  = ReadDouble(signal.Value, "open")  * priceScale;
        double high  = ReadDouble(signal.Value, "high")  * priceScale;
        double low   = ReadDouble(signal.Value, "low")   * priceScale;
        double close = ReadDouble(signal.Value, "close") * priceScale;
        double vol   = ReadDouble(signal.Value, "volume") * volScale;
        double qv    = signal.Value.TryGetProperty("quote_volume", out var qvEl) && qvEl.TryGetDouble(out var qvd)
            ? qvd * volScale
            : (computeQv ? vol * close : 0.0);

        var normalised = new
        {
            exchange   = exchange.ValueKind != JsonValueKind.Undefined ? exchange.GetString() : null,
            symbol     = symbol.ValueKind != JsonValueKind.Undefined   ? symbol.GetString()   : null,
            timeframe  = timeframe.ValueKind != JsonValueKind.Undefined ? timeframe.GetString() : null,
            open_time  = openTime.ValueKind != JsonValueKind.Undefined  ? openTime.GetString()  : null,
            open, high, low, close,
            volume       = vol,
            quote_volume = qv,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "candle_output", BlockSocketType.CandleStream, normalised));
    }

    private static double ReadDouble(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetDouble(out var d) ? d : 0.0;
}
