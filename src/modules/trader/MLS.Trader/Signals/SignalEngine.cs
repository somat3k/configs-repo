using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MLS.Trader.Configuration;
using MLS.Trader.Interfaces;
using MLS.Trader.Models;

namespace MLS.Trader.Signals;

/// <summary>
/// Generates <see cref="TradeSignalResult"/> instances using the <c>model-t</c> ONNX model
/// when available, or a rule-based RSI scorer when the model file is absent.
/// </summary>
/// <remarks>
/// <para>ONNX input tensor shape: <c>[1, 7]</c> with features:</para>
/// <list type="number">
///   <item>RSI ÷ 100 (normalised to [0, 1])</item>
///   <item>MACD value ÷ 100</item>
///   <item>(BollingerUpper − Price) ÷ Price (relative upper band)</item>
///   <item>(BollingerLower − Price) ÷ Price (relative lower band)</item>
///   <item>(BollingerMiddle − Price) ÷ Price (relative middle band)</item>
///   <item>VolumeDelta ÷ 1 000 000 (normalised)</item>
///   <item>Momentum ÷ max(Price, 1) (relative momentum)</item>
/// </list>
/// <para>ONNX output tensor: <c>[1, 3]</c> — class probabilities [Hold, Buy, Sell].
/// Confidence is set to the maximum probability; direction is the argmax class.</para>
/// </remarks>
public sealed class SignalEngine : ISignalEngine, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly IOptions<TraderOptions> _options;
    private readonly ILogger<SignalEngine> _logger;

    // Pre-allocated feature buffer — reused per inference call.
    // SignalEngine is a singleton but GenerateSignalAsync is serialised via the worker's channel.
    private readonly float[] _features = new float[7];

    /// <summary>Initialises a new <see cref="SignalEngine"/>.</summary>
    public SignalEngine(
        IOptions<TraderOptions> options,
        ILogger<SignalEngine> logger)
    {
        _options = options;
        _logger  = logger;

        var modelPath = options.Value.ModelPath;
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            try
            {
                using var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL,
                    InterOpNumThreads      = 1,
                    IntraOpNumThreads      = 1,
                };
                _session = new InferenceSession(modelPath, sessionOptions);
                _logger.LogInformation("SignalEngine: loaded model-t ONNX from {Path}", modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalEngine: failed to load model-t ONNX from {Path} — using rule-based scorer", modelPath);
            }
        }
        else
        {
            _logger.LogInformation("SignalEngine: no ONNX model configured — using rule-based scorer");
        }
    }

    /// <inheritdoc/>
    public ValueTask<TradeSignalResult> GenerateSignalAsync(MarketFeatures features, CancellationToken ct)
    {
        var result = _session is not null
            ? GenerateWithOnnx(features)
            : GenerateWithRules(features);

        return new ValueTask<TradeSignalResult>(result);
    }

    /// <inheritdoc/>
    public void Dispose() => _session?.Dispose();

    // ── ONNX inference ────────────────────────────────────────────────────────────

    private TradeSignalResult GenerateWithOnnx(MarketFeatures f)
    {
        var price = (float)(f.Price == 0m ? 1m : f.Price);

        _features[0] = f.Rsi / 100f;
        _features[1] = f.MacdValue / 100f;
        _features[2] = (f.BollingerUpper - price) / price;
        _features[3] = (f.BollingerLower - price) / price;
        _features[4] = (f.BollingerMiddle - price) / price;
        _features[5] = f.VolumeDelta / 1_000_000f;
        _features[6] = f.Momentum / MathF.Max(price, 1f);

        var inputMeta = _session!.InputMetadata;
        var inputName = inputMeta.Keys.First();

        var tensor = new DenseTensor<float>(_features.AsMemory(), [1, 7]);

        using var inputOrtValue = OrtValue.CreateTensorValueFromMemory(
            OrtMemoryInfo.DefaultInstance, _features.AsMemory(), [1L, 7L]);

        var inputs  = new Dictionary<string, OrtValue> { [inputName] = inputOrtValue };
        var outputs = _session.Run(new RunOptions(), inputs, _session.OutputMetadata.Keys.ToList());

        try
        {
            var probs = outputs[0].GetTensorDataAsSpan<float>();
            // probs[0]=Hold, probs[1]=Buy, probs[2]=Sell
            var maxIdx  = 0;
            var maxProb = probs[0];
            for (var i = 1; i < 3; i++)
            {
                if (probs[i] > maxProb) { maxProb = probs[i]; maxIdx = i; }
            }

            var direction = maxIdx switch
            {
                1 => SignalDirection.Buy,
                2 => SignalDirection.Sell,
                _ => SignalDirection.Hold,
            };

            return new TradeSignalResult(f.Symbol, direction, maxProb, DateTimeOffset.UtcNow);
        }
        finally
        {
            foreach (var o in outputs) o.Dispose();
        }
    }

    // ── Rule-based fallback ───────────────────────────────────────────────────────

    private static TradeSignalResult GenerateWithRules(MarketFeatures f)
    {
        // RSI-based heuristic: oversold → buy, overbought → sell, neutral → hold
        if (f.Rsi < 30f)
            return new TradeSignalResult(f.Symbol, SignalDirection.Buy, 0.72f, DateTimeOffset.UtcNow);

        if (f.Rsi > 70f)
            return new TradeSignalResult(f.Symbol, SignalDirection.Sell, 0.72f, DateTimeOffset.UtcNow);

        return new TradeSignalResult(f.Symbol, SignalDirection.Hold, 0.60f, DateTimeOffset.UtcNow);
    }
}
