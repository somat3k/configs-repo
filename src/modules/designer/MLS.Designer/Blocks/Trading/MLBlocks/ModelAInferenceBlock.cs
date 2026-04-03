using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.MLBlocks;

/// <summary>
/// ML Inference block for the Arbitrage model (model-a).
/// Sends a feature vector to ML Runtime and emits an
/// <see cref="BlockSocketType.ArbitrageOpportunity"/> signal.
/// </summary>
public sealed class ModelAInferenceBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("feature_input", BlockSocketType.FeatureVector),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("ml_output", BlockSocketType.ArbitrageOpportunity),
    ];

    private readonly HttpClient _http;

    private readonly BlockParameter<string> _modelIdParam =
        new("ModelId",             "Model ID",             "ML Runtime model identifier",  "model-a");
    private readonly BlockParameter<float>  _confidenceParam =
        new("ConfidenceThreshold", "Confidence Threshold", "Minimum confidence to emit",   0.75f, MinValue: 0f, MaxValue: 1f, IsOptimizable: true);
    private readonly BlockParameter<int>    _timeoutMsParam =
        new("TimeoutMs",           "Timeout (ms)",         "Inference HTTP timeout",         15, MinValue: 5, MaxValue: 1000);

    /// <inheritdoc/>
    public override string BlockType   => "ModelAInferenceBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Model-A Inference";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_modelIdParam, _confidenceParam, _timeoutMsParam];

    /// <summary>Initialises a new <see cref="ModelAInferenceBlock"/> with the injected HTTP client.</summary>
    public ModelAInferenceBlock(HttpClient http) : base(_inputs, _outputs) => _http = http;

    /// <summary>Default constructor for <see cref="Services.BlockRegistry"/>.</summary>
    public ModelAInferenceBlock() : this(new HttpClient()) { }

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
            var request = new { request_id = Guid.NewGuid(), model_name = _modelIdParam.DefaultValue, features, timeout_ms = _timeoutMsParam.DefaultValue };
            var response = await _http.PostAsJsonAsync("/api/inference", request, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<InferenceResult>(cts.Token).ConfigureAwait(false);
            if (result is null || result.Confidence < _confidenceParam.DefaultValue)
                return null;

            var arbSignal = new { score = result.Confidence, model_name = result.ModelName };
            return EmitObject(BlockId, "ml_output", BlockSocketType.ArbitrageOpportunity, arbSignal);
        }
        catch (OperationCanceledException) { return null; }
    }

    private static float[]? ExtractFeatures(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return null;
        var arr = new float[value.GetArrayLength()];
        var i = 0;
        foreach (var el in value.EnumerateArray()) arr[i++] = el.GetSingle();
        return arr;
    }

    private sealed record InferenceResult(
        [property: JsonPropertyName("request_id")]   string RequestId,
        [property: JsonPropertyName("model_name")]   string ModelName,
        [property: JsonPropertyName("class")]        string Class,
        [property: JsonPropertyName("prediction")]   float Prediction,
        [property: JsonPropertyName("inference_ms")] float InferenceMs)
    {
        [JsonPropertyName("confidence")]
        public float Confidence { get; init; }
    }
}
