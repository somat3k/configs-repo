using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.MLBlocks;

/// <summary>
/// ML Inference block for the Trading model (model-t).
/// Sends a feature vector to the ML Runtime HTTP endpoint and emits a
/// <see cref="BlockSocketType.MLSignal"/> with direction (BUY/SELL/HOLD) and confidence.
/// Target: inference call completes in &lt; 15ms.
/// </summary>
public sealed class ModelTInferenceBlock : BlockBase
{
    private readonly HttpClient _http;

    private readonly BlockParameter<string> _modelIdParam =
        new("ModelId",             "Model ID",            "ML Runtime model identifier",  "model-t");
    private readonly BlockParameter<float>  _confidenceParam =
        new("ConfidenceThreshold", "Confidence Threshold", "Minimum confidence to emit",  0.75f, MinValue: 0f, MaxValue: 1f, IsOptimizable: true);
    private readonly BlockParameter<int>    _timeoutMsParam =
        new("TimeoutMs",           "Timeout (ms)",        "Inference HTTP timeout",        15, MinValue: 5, MaxValue: 1000);

    /// <inheritdoc/>
    public override string BlockType   => "ModelTInferenceBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Model-T Inference";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_modelIdParam, _confidenceParam, _timeoutMsParam];

    /// <summary>Initialises a new <see cref="ModelTInferenceBlock"/> with the injected HTTP client.</summary>
    public ModelTInferenceBlock(HttpClient http) : base(
        [BlockSocket.Input("feature_input", BlockSocketType.FeatureVector)],
        [BlockSocket.Output("ml_output", BlockSocketType.MLSignal)])
        => _http = http;

    private static HttpClient CreateDefaultHttpClient() =>
        new() { BaseAddress = new Uri("http://ml-runtime:5600", UriKind.Absolute) };

    /// <summary>Default constructor used by <see cref="Services.BlockRegistry"/>.</summary>
    public ModelTInferenceBlock() : this(CreateDefaultHttpClient()) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.FeatureVector)
            return null;

        var features = ExtractFeatures(signal.Value);
        if (features is null || features.Length == 0)
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeoutMsParam.DefaultValue);

        try
        {
            var request  = new InferenceRequest(
                RequestId:    Guid.NewGuid().ToString("N"),
                ModelName:    _modelIdParam.DefaultValue,
                Features:     features,
                FeatureNames: [],
                TimeoutMs:    _timeoutMsParam.DefaultValue);

            var response = await _http
                .PostAsJsonAsync("/api/inference", request, cts.Token)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<InferenceResult>(cts.Token)
                .ConfigureAwait(false);

            if (result is null || result.Confidence < _confidenceParam.DefaultValue)
                return null;

            var mlSignal = new MLSignalValue(result.Class, result.Confidence, result.ModelName);
            return EmitObject(BlockId, "ml_output", BlockSocketType.MLSignal, mlSignal);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static float[]? ExtractFeatures(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            var arr = new float[value.GetArrayLength()];
            var i = 0;
            foreach (var el in value.EnumerateArray())
                arr[i++] = el.GetSingle();
            return arr;
        }
        return null;
    }

    // ── Wire types ────────────────────────────────────────────────────────────────

    private sealed record InferenceRequest(
        [property: JsonPropertyName("request_id")]   string RequestId,
        [property: JsonPropertyName("model_name")]   string ModelName,
        [property: JsonPropertyName("features")]     float[] Features,
        [property: JsonPropertyName("feature_names")] string[] FeatureNames,
        [property: JsonPropertyName("timeout_ms")]   int TimeoutMs);

    private sealed record InferenceResult(
        [property: JsonPropertyName("request_id")]  string RequestId,
        [property: JsonPropertyName("model_name")]  string ModelName,
        [property: JsonPropertyName("class")]       string Class,
        [property: JsonPropertyName("prediction")]  float Prediction,
        [property: JsonPropertyName("inference_ms")] float InferenceMs)
    {
        [JsonPropertyName("confidence")]
        public float Confidence { get; init; }
    }

    private sealed record MLSignalValue(
        [property: JsonPropertyName("direction")]  string Direction,
        [property: JsonPropertyName("confidence")] float Confidence,
        [property: JsonPropertyName("model_name")] string ModelName);
}
