using System.Text.Json.Serialization;
using MLS.Core.Tensor;

namespace MLS.Core.Contracts.Tensor;

/// <summary>
/// Payload for <c>TENSOR_BATCH_COMPLETE</c>.
/// Emitted by the ML Runtime or TensorTrainer when a batch of tensors has completed
/// an inference pass or training epoch. Used to trigger downstream consumers and
/// update training progress tracking.
/// </summary>
/// <param name="TraceId">Trace correlation ID shared by all tensors in the batch.</param>
/// <param name="ProducerModuleId">Module that processed the batch (e.g. <c>"ml-runtime"</c>).</param>
/// <param name="BatchSize">Number of tensors in the completed batch.</param>
/// <param name="DType">Declared dtype of the tensors in the batch.</param>
/// <param name="ShapeSummary">Human-readable shape string for a single tensor in the batch (e.g. <c>"[1, 3]"</c>).</param>
/// <param name="BatchId">Unique identifier for this batch, stable across all tensors in the batch.</param>
/// <param name="CompletedAt">UTC time when the batch operation completed.</param>
/// <param name="DurationMs">Total processing duration for the batch in milliseconds.</param>
/// <param name="ModelKey">Optional model key used for the batch (e.g. <c>"model-t"</c>).</param>
public sealed record TensorBatchCompletePayload(
    [property: JsonPropertyName("trace_id")] Guid TraceId,
    [property: JsonPropertyName("producer_module_id")] string ProducerModuleId,
    [property: JsonPropertyName("batch_size")] int BatchSize,
    [property: JsonPropertyName("dtype")] TensorDType DType,
    [property: JsonPropertyName("shape_summary")] string ShapeSummary,
    [property: JsonPropertyName("batch_id")] Guid BatchId,
    [property: JsonPropertyName("completed_at")] DateTimeOffset CompletedAt,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("model_key")] string? ModelKey);
