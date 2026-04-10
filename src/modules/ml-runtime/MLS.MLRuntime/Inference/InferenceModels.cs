using System.Text.Json.Serialization;

namespace MLS.MLRuntime.Inference;

/// <summary>
/// Payload for <c>INFERENCE_REQUEST</c> — sent by any module to ml-runtime
/// requesting a single forward-pass through an ONNX model.
/// </summary>
/// <param name="RequestId">Unique identifier for this inference call (for correlation).</param>
/// <param name="ModelKey">Registry key: <c>model-t</c>, <c>model-a</c>, or <c>model-d</c>.</param>
/// <param name="Features">Flat feature vector fed into the ONNX model input tensor.</param>
/// <param name="RequesterModuleId">Module ID of the caller (for routing the result back).</param>
public sealed record InferenceRequestPayload(
    [property: JsonPropertyName("request_id")]         Guid     RequestId,
    [property: JsonPropertyName("model_key")]          string   ModelKey,
    [property: JsonPropertyName("features")]           float[]  Features,
    [property: JsonPropertyName("requester_module_id")] string  RequesterModuleId);

/// <summary>
/// Payload for <c>INFERENCE_RESULT</c> — returned by ml-runtime after a successful
/// forward-pass through the requested ONNX model.
/// </summary>
/// <param name="RequestId">Correlation ID matching the originating <see cref="InferenceRequestPayload.RequestId"/>.</param>
/// <param name="ModelKey">Registry key of the model that produced the result.</param>
/// <param name="Outputs">Raw ONNX output tensor values (flat array).</param>
/// <param name="LatencyMs">Wall-clock milliseconds the inference took.</param>
/// <param name="Cached">Whether this result was served from the Redis cache.</param>
/// <param name="ModelId">Versioned model identifier, if known.</param>
public sealed record InferenceResultPayload(
    [property: JsonPropertyName("request_id")]  Guid     RequestId,
    [property: JsonPropertyName("model_key")]   string   ModelKey,
    [property: JsonPropertyName("outputs")]     float[]  Outputs,
    [property: JsonPropertyName("latency_ms")]  double   LatencyMs,
    [property: JsonPropertyName("cached")]      bool     Cached,
    [property: JsonPropertyName("model_id")]    string?  ModelId);
