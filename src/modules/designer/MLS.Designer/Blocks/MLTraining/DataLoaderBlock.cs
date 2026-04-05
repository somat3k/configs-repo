using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Data loader block — accumulates raw OHLCV <see cref="BlockSocketType.CandleStream"/> signals
/// into a fixed-size rolling buffer and emits a <see cref="BlockSocketType.FeatureVector"/>
/// batch when the buffer reaches the configured <c>WindowSize</c> candles.
/// <para>
/// This block is the entry point of the ML training pipeline. Upstream data source blocks
/// (e.g. <c>BacktestReplayBlock</c>) feed historical OHLCV candles here; the batched output
/// is then passed to <c>FeatureEngineerBlock</c> for indicator computation.
/// </para>
/// </summary>
/// <remarks>
/// Expected candle signal payload: <c>{ symbol?, exchange?, open, high, low, close, volume }</c>.
/// When <c>symbol</c> or <c>exchange</c> are present in the payload they are matched against
/// the block parameters; non-matching candles are silently dropped.
/// </remarks>
public sealed class DataLoaderBlock : BlockBase
{
    private readonly List<float[]> _buffer = [];

    private readonly BlockParameter<string> _symbolParam =
        new("Symbol",    "Symbol",    "Trading symbol to accept (e.g. BTC-PERP). Leave empty to accept all.", "BTC-PERP");
    private readonly BlockParameter<string> _exchangeParam =
        new("Exchange",  "Exchange",  "Source exchange to accept (e.g. hyperliquid). Leave empty to accept all.", "hyperliquid");
    private readonly BlockParameter<int> _windowSizeParam =
        new("WindowSize","Window Size","Number of candles to accumulate before emitting a batch",
            512, MinValue: 64, MaxValue: 4096);
    private readonly BlockParameter<string> _modelTypeParam =
        new("ModelType", "Model Type","Target model registry key: model-t, model-a, or model-d", "model-t");

    /// <inheritdoc/>
    public override string BlockType   => "DataLoaderBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Data Loader";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_symbolParam, _exchangeParam, _windowSizeParam, _modelTypeParam];

    /// <summary>Initialises a new <see cref="DataLoaderBlock"/>.</summary>
    public DataLoaderBlock() : base(
        [BlockSocket.Input("candle_input",  BlockSocketType.CandleStream)],
        [BlockSocket.Output("feature_output", BlockSocketType.FeatureVector)]) { }

    /// <inheritdoc/>
    public override void Reset() => _buffer.Clear();

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.CandleStream)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractOhlcv(signal.Value, out var ohlcv, out var symbolInPayload, out var exchangeInPayload))
            return new ValueTask<BlockSignal?>(result: null);

        // Filter by symbol if both the parameter and the payload carry a non-empty value
        var filterSymbol   = _symbolParam.DefaultValue;
        var filterExchange = _exchangeParam.DefaultValue;

        if (!string.IsNullOrEmpty(filterSymbol) &&
            !string.IsNullOrEmpty(symbolInPayload) &&
            !symbolInPayload.Equals(filterSymbol, StringComparison.OrdinalIgnoreCase))
            return new ValueTask<BlockSignal?>(result: null);

        if (!string.IsNullOrEmpty(filterExchange) &&
            !string.IsNullOrEmpty(exchangeInPayload) &&
            !exchangeInPayload.Equals(filterExchange, StringComparison.OrdinalIgnoreCase))
            return new ValueTask<BlockSignal?>(result: null);

        _buffer.Add(ohlcv);

        if (_buffer.Count < _windowSizeParam.DefaultValue)
            return new ValueTask<BlockSignal?>(result: null);

        var batch = _buffer.ToArray();
        _buffer.Clear();

        var featureBatch = new FeatureBatch(
            ModelType:  _modelTypeParam.DefaultValue,
            Symbol:     filterSymbol,
            Exchange:   filterExchange,
            WindowSize: batch.Length,
            Samples:    batch);

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "feature_output", BlockSocketType.FeatureVector, featureBatch));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static bool TryExtractOhlcv(
        JsonElement value,
        out float[] ohlcv,
        out string  symbol,
        out string  exchange)
    {
        ohlcv    = [];
        symbol   = string.Empty;
        exchange = string.Empty;

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (!value.TryGetProperty("open",   out var o) ||
            !value.TryGetProperty("high",   out var h) ||
            !value.TryGetProperty("low",    out var l) ||
            !value.TryGetProperty("close",  out var c) ||
            !value.TryGetProperty("volume", out var v))
            return false;

        if (value.TryGetProperty("symbol",   out var sym)) symbol   = sym.GetString() ?? string.Empty;
        if (value.TryGetProperty("exchange", out var ex))  exchange = ex.GetString()  ?? string.Empty;

        ohlcv = [o.GetSingle(), h.GetSingle(), l.GetSingle(), c.GetSingle(), v.GetSingle()];
        return true;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    internal sealed record FeatureBatch(
        [property: JsonPropertyName("model_type")]  string    ModelType,
        [property: JsonPropertyName("symbol")]      string    Symbol,
        [property: JsonPropertyName("exchange")]    string    Exchange,
        [property: JsonPropertyName("window_size")] int       WindowSize,
        [property: JsonPropertyName("samples")]     float[][] Samples);
}
