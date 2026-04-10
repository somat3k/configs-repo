using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Scoring;

/// <summary>
/// Opportunity scorer that runs the model-a ONNX artefact when available,
/// falling back to a rule-based confidence estimate otherwise.
/// </summary>
/// <remarks>
/// <para>
/// ONNX input tensor shape: <c>[1, 6]</c> with features:
/// <list type="number">
///   <item>netProfitUsd (normalised ÷ 1000)</item>
///   <item>profitRatio (raw fraction)</item>
///   <item>hopCount (integer cast to float)</item>
///   <item>gasEstimateUsd (normalised ÷ 10)</item>
///   <item>inputAmountUsd (normalised ÷ 10000)</item>
///   <item>ttlSeconds (time to expiry, normalised ÷ 60)</item>
/// </list>
/// </para>
/// <para>
/// ONNX output tensor: <c>[1, 2]</c> — class probabilities [reject, accept];
/// confidence = output[0, 1].
/// </para>
/// </remarks>
public sealed class OpportunityScorer : IOpportunityScorer, IDisposable
{
    private readonly InferenceSession? _session;
    private readonly IOptions<ArbitragerOptions> _options;
    private readonly ILogger<OpportunityScorer> _logger;

    // Pre-allocated feature buffer (reused per call — not thread-safe; scorer is singleton
    // but ScoreAsync is effectively serialised via the executor's channel).
    private readonly float[] _features = new float[6];

    /// <summary>Initialises a new <see cref="OpportunityScorer"/>.</summary>
    public OpportunityScorer(
        IOptions<ArbitragerOptions> options,
        ILogger<OpportunityScorer> logger)
    {
        _options = options;
        _logger  = logger;

        var modelPath = options.Value.ModelPath;
        if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL,
                    InterOpNumThreads      = 1,
                    IntraOpNumThreads      = 1,
                };
                _session = new InferenceSession(modelPath, sessionOptions);
                _logger.LogInformation("OpportunityScorer: loaded ONNX model from {Path}", modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpportunityScorer: failed to load ONNX model from {Path} — using rule-based scorer.", modelPath);
            }
        }
        else
        {
            _logger.LogInformation("OpportunityScorer: no ONNX model configured — using rule-based scorer.");
        }
    }

    /// <inheritdoc/>
    public ValueTask<float> ScoreAsync(ArbitrageOpportunity opportunity, CancellationToken ct)
    {
        var score = _session is not null
            ? ScoreWithOnnx(opportunity)
            : ScoreWithRules(opportunity);

        return new ValueTask<float>(score);
    }

    // ── ONNX inference ────────────────────────────────────────────────────────

    private float ScoreWithOnnx(ArbitrageOpportunity o)
    {
        BuildFeatures(o);

        var tensor = new DenseTensor<float>(_features, [1, 6]);
        using var inputs = new DisposableNamedOnnxValueList(
        [
            NamedOnnxValue.CreateFromTensor("features", tensor),
        ]);

        try
        {
            using var results = _session!.Run(inputs);
            var output = results[0].AsEnumerable<float>().ToArray();
            // output shape [1,2]: [P(reject), P(accept)]
            if (output.Length >= 2) return output[1];
            if (output.Length == 1) return output[0];

            _logger.LogWarning("OpportunityScorer: ONNX inference returned an empty output tensor — falling back to rule-based score.");
            return ScoreWithRules(o);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpportunityScorer: ONNX inference failed — falling back to rule-based score.");
            return ScoreWithRules(o);
        }
    }

    // ── Rule-based fallback ───────────────────────────────────────────────────

    private static float ScoreWithRules(ArbitrageOpportunity o)
    {
        // Simple linear heuristic combining profit ratio, hop count penalty and gas efficiency.
        var profitScore    = Math.Min((float)(o.ProfitRatio * 20m), 1f);   // 5% ratio → 1.0
        var hopPenalty     = 1f - (o.Hops.Count - 2) * 0.1f;              // penalty per extra hop
        var gasEfficiency  = o.NetProfitUsd > 0
            ? Math.Min((float)(o.NetProfitUsd / (o.GasEstimateUsd + 0.01m)), 10f) / 10f
            : 0f;

        var raw = profitScore * 0.5f + Math.Max(hopPenalty, 0f) * 0.3f + gasEfficiency * 0.2f;
        return Math.Clamp(raw, 0f, 1f);
    }

    // ── Feature construction ──────────────────────────────────────────────────

    private void BuildFeatures(ArbitrageOpportunity o)
    {
        var ttlSeconds = Math.Max((o.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, 0);

        _features[0] = (float)(o.NetProfitUsd / 1_000m);
        _features[1] = (float)o.ProfitRatio;
        _features[2] = o.Hops.Count;
        _features[3] = (float)(o.GasEstimateUsd / 10m);
        _features[4] = (float)(o.InputAmountUsd / 10_000m);
        _features[5] = (float)(ttlSeconds / 60.0);
    }

    /// <inheritdoc/>
    public void Dispose() => _session?.Dispose();
}

// Minimal helper to satisfy IDisposable pattern for ONNX input list
file sealed class DisposableNamedOnnxValueList(List<NamedOnnxValue> values)
    : List<NamedOnnxValue>(values), IDisposable
{
    public void Dispose() { }
}
