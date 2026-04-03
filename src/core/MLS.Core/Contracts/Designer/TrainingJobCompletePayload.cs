using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>TRAINING_JOB_COMPLETE</c> — emitted by Shell VM when training finishes
/// and sent to both Designer and ml-runtime so ONNX inference can be hot-reloaded.
/// </summary>
/// <param name="JobId">Training job identifier.</param>
/// <param name="ModelType">Model registry key: <c>model-t</c>, <c>model-a</c>, or <c>model-d</c>.</param>
/// <param name="ModelId">New versioned model identifier, e.g. <c>model-t-v3.1</c>.</param>
/// <param name="OnnxPath">Container-local path to the ONNX artefact.</param>
/// <param name="JoblibPath">Container-local path to the JOBLIB serialised pipeline.</param>
/// <param name="IpfsCid">IPFS CID after upload — used for distributed artefact retrieval.</param>
/// <param name="Metrics">Final training metrics (JSON object: f1_macro, accuracy, val_sharpe, …).</param>
/// <param name="DurationMs">Total training wall-clock duration in milliseconds.</param>
public sealed record TrainingJobCompletePayload(
    [property: JsonPropertyName("job_id")] Guid JobId,
    [property: JsonPropertyName("model_type")] string ModelType,
    [property: JsonPropertyName("model_id")] string ModelId,
    [property: JsonPropertyName("onnx_path")] string OnnxPath,
    [property: JsonPropertyName("joblib_path")] string JoblibPath,
    [property: JsonPropertyName("ipfs_cid")] string IpfsCid,
    [property: JsonPropertyName("metrics")] JsonElement Metrics,
    [property: JsonPropertyName("duration_ms")] long DurationMs);
