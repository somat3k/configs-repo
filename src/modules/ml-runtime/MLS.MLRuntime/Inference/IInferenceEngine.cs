namespace MLS.MLRuntime.Inference;

/// <summary>
/// Runs a single ONNX forward-pass for a given model key, with Redis result caching.
/// </summary>
public interface IInferenceEngine
{
    /// <summary>
    /// Executes inference using the model identified by <see cref="InferenceRequestPayload.ModelKey"/>.
    /// Results are cached in Redis for <c>MLRuntimeOptions.RedisCacheTtlSeconds</c> seconds.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested model is not loaded in the registry.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when inference does not complete within <c>MLRuntimeOptions.InferenceTimeoutMs</c>.
    /// </exception>
    ValueTask<InferenceResultPayload> RunAsync(InferenceRequestPayload request, CancellationToken ct = default);
}
