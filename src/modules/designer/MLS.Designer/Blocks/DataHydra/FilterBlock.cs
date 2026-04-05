using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DataHydra;

/// <summary>
/// Filter block — applies configurable criteria to an incoming candle stream and
/// forwards only candles that pass all configured predicates.
/// </summary>
/// <remarks>
/// <para>
/// Supported filter dimensions:
/// <list type="bullet">
///   <item><b>Symbol</b> — exact match (case-insensitive) or wildcard <c>"*"</c> to accept all.</item>
///   <item><b>Timeframe</b> — exact match (e.g. <c>"1m"</c>, <c>"1h"</c>) or <c>"*"</c>.</item>
///   <item><b>MinVolume</b> — candles with quote volume below this threshold are dropped.</item>
/// </list>
/// </para>
/// <para>
/// Input:  <see cref="BlockSocketType.CandleStream"/>. <br/>
/// Output: <see cref="BlockSocketType.CandleStream"/> — filtered subset.
/// </para>
/// </remarks>
public sealed class FilterBlock : BlockBase
{
    private readonly BlockParameter<string>  _symbolFilterParam =
        new("SymbolFilter",    "Symbol Filter",    "Symbol to accept; use '*' for all",       "*");
    private readonly BlockParameter<string>  _timeframeFilterParam =
        new("TimeframeFilter", "Timeframe Filter", "Timeframe to accept; use '*' for all",    "*");
    private readonly BlockParameter<decimal> _minVolumeParam =
        new("MinVolume",       "Min Volume",       "Minimum quote volume threshold (0 = all)", 0m,
            MinValue: 0m, MaxValue: decimal.MaxValue);

    /// <inheritdoc/>
    public override string BlockType   => "FilterBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Filter";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_symbolFilterParam, _timeframeFilterParam, _minVolumeParam];

    /// <summary>Initialises a <see cref="FilterBlock"/>.</summary>
    public FilterBlock() : base(
        [BlockSocket.Input("candle_input",  BlockSocketType.CandleStream)],
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

        var symFilter = _symbolFilterParam.DefaultValue;
        var tfFilter  = _timeframeFilterParam.DefaultValue;
        var minVol    = _minVolumeParam.DefaultValue;

        // Symbol filter
        if (symFilter != "*" &&
            signal.Value.TryGetProperty("symbol", out var symEl) &&
            !string.Equals(symEl.GetString(), symFilter, StringComparison.OrdinalIgnoreCase))
            return new ValueTask<BlockSignal?>(result: null);

        // Timeframe filter
        if (tfFilter != "*" &&
            signal.Value.TryGetProperty("timeframe", out var tfEl) &&
            !string.Equals(tfEl.GetString(), tfFilter, StringComparison.OrdinalIgnoreCase))
            return new ValueTask<BlockSignal?>(result: null);

        // Volume filter
        if (minVol > 0m &&
            signal.Value.TryGetProperty("quote_volume", out var volEl) &&
            volEl.TryGetDecimal(out var vol) &&
            vol < minVol)
            return new ValueTask<BlockSignal?>(result: null);

        return new ValueTask<BlockSignal?>(
            new BlockSignal(BlockId, "candle_output", BlockSocketType.CandleStream, signal.Value));
    }
}
