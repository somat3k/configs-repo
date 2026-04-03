using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.MLTraining;

/// <summary>
/// Feature engineering block — receives a raw OHLCV <see cref="BlockSocketType.FeatureVector"/>
/// batch from <c>DataLoaderBlock</c> and emits an engineered feature vector aligned to the
/// <c>model-t</c> feature schema (RSI, MACD, Bollinger Bands, ATR, VWAP, Volume delta,
/// Spread BPS, VWAP distance).
/// </summary>
/// <remarks>
/// Output feature order matches <c>model-t</c> / <c>model-a</c> shared schema:
/// <code>
/// [0] rsi_14         – Wilder RSI normalised to [0, 1]
/// [1] macd_signal    – MACD histogram (MACD line − signal line), z-score
/// [2] bb_position    – Price position within Bollinger Bands [0, 1]
/// [3] volume_delta   – Relative volume vs rolling avg, z-score
/// [4] momentum_20    – 20-period price momentum, z-score
/// [5] atr_14         – Average True Range, z-score
/// [6] spread_bps     – High−Low spread in basis points [0, ∞)
/// [7] vwap_distance  – Distance from VWAP, z-score
/// </code>
/// Samples with insufficient warm-up history are silently discarded.
/// </remarks>
public sealed class FeatureEngineerBlock : BlockBase
{
    private readonly BlockParameter<int> _rsiPeriodParam =
        new("RsiPeriod",    "RSI Period",     "Wilder RSI period",          14, MinValue: 2, MaxValue: 50);
    private readonly BlockParameter<int> _macdFastParam =
        new("MacdFast",     "MACD Fast",      "MACD fast EMA period",       12, MinValue: 2, MaxValue: 50);
    private readonly BlockParameter<int> _macdSlowParam =
        new("MacdSlow",     "MACD Slow",      "MACD slow EMA period",       26, MinValue: 5, MaxValue: 200);
    private readonly BlockParameter<int> _macdSignalParam =
        new("MacdSignal",   "MACD Signal",    "MACD signal EMA period",     9,  MinValue: 2, MaxValue: 50);
    private readonly BlockParameter<int> _bbPeriodParam =
        new("BbPeriod",     "BB Period",      "Bollinger Bands period",     20, MinValue: 5, MaxValue: 100);
    private readonly BlockParameter<int> _atrPeriodParam =
        new("AtrPeriod",    "ATR Period",     "Average True Range period",  14, MinValue: 2, MaxValue: 50);
    private readonly BlockParameter<int> _vwapPeriodParam =
        new("VwapPeriod",   "VWAP Period",    "VWAP rolling window",        20, MinValue: 5, MaxValue: 200);
    private readonly BlockParameter<int> _volAvgPeriodParam =
        new("VolAvgPeriod", "Vol Avg Period", "Volume rolling average window", 20, MinValue: 5, MaxValue: 200);

    /// <inheritdoc/>
    public override string BlockType   => "FeatureEngineerBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Feature Engineer";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_rsiPeriodParam, _macdFastParam, _macdSlowParam, _macdSignalParam,
         _bbPeriodParam, _atrPeriodParam, _vwapPeriodParam, _volAvgPeriodParam];

    /// <summary>Initialises a new <see cref="FeatureEngineerBlock"/>.</summary>
    public FeatureEngineerBlock() : base(
        [BlockSocket.Input("raw_features", BlockSocketType.FeatureVector)],
        [BlockSocket.Output("engineered_features", BlockSocketType.FeatureVector)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.FeatureVector)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractBatch(signal.Value, out var modelType, out var symbol, out var exchange, out var samples))
            return new ValueTask<BlockSignal?>(result: null);

        var engineered = ComputeFeatures(samples);
        if (engineered.Length == 0)
            return new ValueTask<BlockSignal?>(result: null);

        var featureBatch = new EngineeredBatch(
            ModelType:    modelType,
            Symbol:       symbol,
            Exchange:     exchange,
            FeatureNames: FeatureNames,
            Samples:      engineered);

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "engineered_features", BlockSocketType.FeatureVector, featureBatch));
    }

    // ── Feature computation ───────────────────────────────────────────────────────

    private static readonly string[] FeatureNames =
        ["rsi_14", "macd_signal", "bb_position", "volume_delta", "momentum_20", "atr_14", "spread_bps", "vwap_distance"];

    private float[][] ComputeFeatures(float[][] ohlcv)
    {
        // ohlcv columns: [open, high, low, close, volume]
        int n = ohlcv.Length;
        int warmup = Math.Max(_macdSlowParam.DefaultValue + _macdSignalParam.DefaultValue,
                         Math.Max(_bbPeriodParam.DefaultValue, _vwapPeriodParam.DefaultValue)) + 1;

        if (n < warmup) return [];

        var closes  = ohlcv.Select(r => r[3]).ToArray();
        var highs   = ohlcv.Select(r => r[1]).ToArray();
        var lows    = ohlcv.Select(r => r[2]).ToArray();
        var volumes = ohlcv.Select(r => r[4]).ToArray();

        var rsi        = ComputeRsi(closes, _rsiPeriodParam.DefaultValue);
        var macdHist   = ComputeMacdHistogram(closes, _macdFastParam.DefaultValue,
                             _macdSlowParam.DefaultValue, _macdSignalParam.DefaultValue);
        var bbPos      = ComputeBbPosition(closes, _bbPeriodParam.DefaultValue);
        var volDelta   = ComputeZScore(volumes, _volAvgPeriodParam.DefaultValue);
        var momentum20 = ComputeMomentum(closes, 20);
        var atr        = ComputeAtr(highs, lows, closes, _atrPeriodParam.DefaultValue);
        var spreadBps  = ComputeSpreadBps(highs, lows, closes);
        var vwapDist   = ComputeVwapDistance(highs, lows, closes, volumes, _vwapPeriodParam.DefaultValue);

        var result = new List<float[]>(n);
        for (int i = 0; i < n; i++)
        {
            if (float.IsNaN(rsi[i]) || float.IsNaN(macdHist[i]) || float.IsNaN(bbPos[i]))
                continue;

            result.Add([rsi[i], macdHist[i], bbPos[i], volDelta[i],
                        momentum20[i], atr[i], spreadBps[i], vwapDist[i]]);
        }

        return [.. result];
    }

    // ── Indicator implementations ─────────────────────────────────────────────────

    private static float[] ComputeRsi(float[] closes, int period)
    {
        var rsi = new float[closes.Length];
        if (closes.Length <= period) return FillNaN(rsi);

        double avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            double change = closes[i] - closes[i - 1];
            if (change > 0) avgGain += change;
            else avgLoss += -change;
        }
        avgGain /= period;
        avgLoss /= period;

        for (int i = 0; i < period; i++)
            rsi[i] = float.NaN;

        rsi[period] = avgLoss < 1e-10 ? 1f : (float)(avgGain / (avgGain + avgLoss));

        for (int i = period + 1; i < closes.Length; i++)
        {
            double change = closes[i] - closes[i - 1];
            double gain   = change > 0 ? change : 0;
            double loss   = change < 0 ? -change : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            rsi[i] = avgLoss < 1e-10 ? 1f : (float)(avgGain / (avgGain + avgLoss));
        }

        return rsi;
    }

    private static float[] ComputeMacdHistogram(float[] closes, int fast, int slow, int signal)
    {
        var fastEma   = ComputeEma(closes, fast);
        var slowEma   = ComputeEma(closes, slow);
        var macdLine  = new float[closes.Length];

        for (int i = 0; i < closes.Length; i++)
            macdLine[i] = float.IsNaN(fastEma[i]) || float.IsNaN(slowEma[i])
                ? float.NaN
                : fastEma[i] - slowEma[i];

        var signalLine = ComputeEma(macdLine, signal);
        var hist       = new float[closes.Length];

        for (int i = 0; i < closes.Length; i++)
            hist[i] = float.IsNaN(macdLine[i]) || float.IsNaN(signalLine[i])
                ? float.NaN
                : macdLine[i] - signalLine[i];

        return ZScoreNonNaN(hist);
    }

    private static float[] ComputeEma(float[] values, int period)
    {
        var ema = new float[values.Length];
        double k = 2.0 / (period + 1);

        int start = -1;
        for (int i = 0; i < values.Length; i++)
            if (!float.IsNaN(values[i])) { start = i; break; }

        if (start < 0) return FillNaN(ema);

        for (int i = 0; i < start + period; i++)
            ema[i] = float.NaN;

        if (start + period > values.Length) return FillNaN(ema);

        double seed = 0;
        for (int i = start; i < start + period; i++) seed += values[i];
        seed /= period;
        ema[start + period - 1] = (float)seed;

        for (int i = start + period; i < values.Length; i++)
            ema[i] = (float)(values[i] * k + ema[i - 1] * (1 - k));

        return ema;
    }

    private static float[] ComputeBbPosition(float[] closes, int period)
    {
        var result = new float[closes.Length];
        for (int i = 0; i < Math.Min(period - 1, closes.Length); i++) result[i] = float.NaN;

        for (int i = period - 1; i < closes.Length; i++)
        {
            double mean = 0;
            for (int j = i - period + 1; j <= i; j++) mean += closes[j];
            mean /= period;

            double variance = 0;
            for (int j = i - period + 1; j <= i; j++)
                variance += (closes[j] - mean) * (closes[j] - mean);
            double std = Math.Sqrt(variance / period);

            double upper = mean + 2 * std;
            double lower = mean - 2 * std;
            double range = upper - lower;

            result[i] = range < 1e-10 ? 0.5f : (float)Math.Clamp((closes[i] - lower) / range, 0.0, 1.0);
        }

        return result;
    }

    private static float[] ComputeZScore(float[] values, int period)
    {
        var result = new float[values.Length];
        for (int i = 0; i < Math.Min(period - 1, values.Length); i++) result[i] = 0f;

        for (int i = period - 1; i < values.Length; i++)
        {
            double mean = 0;
            for (int j = i - period + 1; j <= i; j++) mean += values[j];
            mean /= period;

            double variance = 0;
            for (int j = i - period + 1; j <= i; j++)
                variance += (values[j] - mean) * (values[j] - mean);
            double std = Math.Sqrt(variance / period);

            result[i] = std < 1e-10 ? 0f : (float)((values[i] - mean) / std);
        }

        return result;
    }

    private static float[] ComputeMomentum(float[] closes, int period)
    {
        var result = new float[closes.Length];
        for (int i = 0; i < Math.Min(period, closes.Length); i++) result[i] = 0f;

        for (int i = period; i < closes.Length; i++)
            result[i] = closes[i - period] > 1e-10f
                ? (float)((closes[i] / closes[i - period]) - 1.0)
                : 0f;

        return ZScoreNonNaN(result);
    }

    private static float[] ComputeAtr(float[] highs, float[] lows, float[] closes, int period)
    {
        var tr  = new float[closes.Length];
        tr[0]   = highs[0] - lows[0];

        for (int i = 1; i < closes.Length; i++)
        {
            float hl = highs[i]  - lows[i];
            float hc = Math.Abs(highs[i]  - closes[i - 1]);
            float lc = Math.Abs(lows[i]   - closes[i - 1]);
            tr[i] = Math.Max(hl, Math.Max(hc, lc));
        }

        var atr = new float[closes.Length];
        double sum = 0;
        for (int i = 0; i < Math.Min(period, closes.Length); i++) sum += tr[i];
        for (int i = 0; i < Math.Min(period - 1, closes.Length); i++) atr[i] = float.NaN;

        if (period <= closes.Length)
        {
            atr[period - 1] = (float)(sum / period);
            for (int i = period; i < closes.Length; i++)
                atr[i] = (float)((atr[i - 1] * (period - 1) + tr[i]) / period);
        }

        return ZScoreNonNaN(atr);
    }

    private static float[] ComputeSpreadBps(float[] highs, float[] lows, float[] closes)
    {
        var result = new float[closes.Length];
        for (int i = 0; i < closes.Length; i++)
            result[i] = closes[i] > 1e-10f
                ? (highs[i] - lows[i]) / closes[i] * 10_000f
                : 0f;
        return result;
    }

    private static float[] ComputeVwapDistance(
        float[] highs, float[] lows, float[] closes, float[] volumes, int period)
    {
        var result = new float[closes.Length];
        for (int i = period - 1; i < closes.Length; i++)
        {
            double sumPV = 0, sumV = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                double typical = (highs[j] + lows[j] + closes[j]) / 3.0;
                sumPV += typical * volumes[j];
                sumV  += volumes[j];
            }

            double vwap = sumV > 1e-10 ? sumPV / sumV : closes[i];
            result[i] = vwap > 1e-10 ? (float)((closes[i] - vwap) / vwap) : 0f;
        }

        return ZScoreNonNaN(result);
    }

    private static float[] ZScoreNonNaN(float[] values)
    {
        var nonNaN = values.Where(v => !float.IsNaN(v)).ToArray();
        if (nonNaN.Length < 2) return values;

        double mean = nonNaN.Average();
        double std  = Math.Sqrt(nonNaN.Sum(v => (v - mean) * (v - mean)) / nonNaN.Length);
        if (std < 1e-10) return values;

        return values.Select(v => float.IsNaN(v) ? v : (float)((v - mean) / std)).ToArray();
    }

    private static float[] FillNaN(float[] arr) { Array.Fill(arr, float.NaN); return arr; }

    // ── Extraction helpers ────────────────────────────────────────────────────────

    private static bool TryExtractBatch(
        JsonElement value,
        out string modelType, out string symbol, out string exchange,
        out float[][] samples)
    {
        modelType = "model-t";
        symbol    = string.Empty;
        exchange  = string.Empty;
        samples   = [];

        if (value.ValueKind != JsonValueKind.Object) return false;

        if (value.TryGetProperty("model_type", out var mt)) modelType = mt.GetString() ?? "model-t";
        if (value.TryGetProperty("symbol",     out var sy)) symbol    = sy.GetString() ?? string.Empty;
        if (value.TryGetProperty("exchange",   out var ex)) exchange  = ex.GetString() ?? string.Empty;

        if (!value.TryGetProperty("samples", out var samplesEl) ||
            samplesEl.ValueKind != JsonValueKind.Array)
            return false;

        var list = new List<float[]>();
        foreach (var row in samplesEl.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) continue;
            var arr = new float[row.GetArrayLength()];
            int idx = 0;
            foreach (var el in row.EnumerateArray()) arr[idx++] = el.GetSingle();
            list.Add(arr);
        }

        samples = [.. list];
        return samples.Length > 0;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    internal sealed record EngineeredBatch(
        [property: JsonPropertyName("model_type")]    string   ModelType,
        [property: JsonPropertyName("symbol")]        string   Symbol,
        [property: JsonPropertyName("exchange")]      string   Exchange,
        [property: JsonPropertyName("feature_names")] string[] FeatureNames,
        [property: JsonPropertyName("samples")]       float[][] Samples);
}
