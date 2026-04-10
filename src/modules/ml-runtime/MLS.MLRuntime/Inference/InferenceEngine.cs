using System.Diagnostics;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MLS.MLRuntime.Configuration;
using MLS.MLRuntime.Models;
using StackExchange.Redis;

namespace MLS.MLRuntime.Inference;

/// <summary>
/// Runs ONNX inference via <see cref="IModelRegistry"/> and caches results in Redis.
/// Inference is executed on a thread-pool thread (<c>Task.Run</c>) because
/// <see cref="InferenceSession"/> uses a synchronous blocking <c>Run</c> call.
/// Concurrency is bounded by <see cref="MLRuntimeOptions.MaxConcurrentInferences"/>.
/// </summary>
public sealed class InferenceEngine : IInferenceEngine, IDisposable
{
    private readonly IModelRegistry _registry;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IOptions<MLRuntimeOptions> _options;
    private readonly ILogger<InferenceEngine> _logger;
    private readonly SemaphoreSlim _throttle;

    /// <summary>Initialises a new <see cref="InferenceEngine"/>.</summary>
    public InferenceEngine(
        IModelRegistry registry,
        IConnectionMultiplexer? redis,
        IOptions<MLRuntimeOptions> options,
        ILogger<InferenceEngine> logger)
    {
        _registry = registry;
        _redis    = redis;
        _options  = options;
        _logger   = logger;
        _throttle = new SemaphoreSlim(Math.Max(1, options.Value.MaxConcurrentInferences));
    }

    /// <inheritdoc/>
    public void Dispose() => _throttle.Dispose();

    /// <inheritdoc/>
    public async ValueTask<InferenceResultPayload> RunAsync(
        InferenceRequestPayload request, CancellationToken ct = default)
    {
        // 1. Resolve session first — needed to build a version-aware cache key that
        //    automatically invalidates after a hot-reload (new ModelId or LoadedAt).
        var record = await _registry.GetAsync(request.ModelKey, ct).ConfigureAwait(false);
        if (record is null)
            throw new InvalidOperationException($"Model not loaded: {request.ModelKey}");

        // 2. Versioned cache key — includes the model version/timestamp so Redis never
        //    returns stale results from a previous model after hot-reload.
        var modelVersion = record.ModelId ?? record.LoadedAt.Ticks.ToString("X");
        var cacheKey     = BuildCacheKey(request.ModelKey, modelVersion, request.Features);

        // 3. Try Redis cache
        var cached = await TryGetCachedAsync(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null)
            return cached with { RequestId = request.RequestId, Cached = true };

        // 4. Run inference — bounded by MaxConcurrentInferences semaphore
        await _throttle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var sw        = Stopwatch.StartNew();
            var timeoutMs = _options.Value.InferenceTimeoutMs;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            float[] outputs;
            try
            {
                outputs = await Task.Run(() => RunOnnx(record.Session, request.Features), timeoutCts.Token)
                                     .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Inference for model '{request.ModelKey}' exceeded {timeoutMs} ms.");
            }

            sw.Stop();

            var result = new InferenceResultPayload(
                RequestId:  request.RequestId,
                ModelKey:   request.ModelKey,
                Outputs:    outputs,
                LatencyMs:  sw.Elapsed.TotalMilliseconds,
                Cached:     false,
                ModelId:    record.ModelId);

            // 5. Cache result (fire-and-forget)
            _ = CacheResultAsync(cacheKey, result, _options.Value.RedisCacheTtlSeconds);

            return result;
        }
        finally
        {
            _throttle.Release();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Runs a forward pass. Resolves input/output names dynamically from the session's
    /// metadata so any ONNX model topology is supported, and flattens all outputs.
    /// For multi-input topologies the first input in metadata order is used; models with
    /// multiple inputs are not currently supported and will throw at this line.
    /// </summary>
    private static float[] RunOnnx(InferenceSession session, float[] features)
    {
        var inputName = session.InputMetadata.Keys.FirstOrDefault()
            ?? throw new InvalidOperationException("ONNX session exposes no inputs.");

        if (session.InputMetadata.Count > 1)
            throw new InvalidOperationException(
                $"Multi-input ONNX models are not supported. " +
                $"Session exposes {session.InputMetadata.Count} inputs: " +
                string.Join(", ", session.InputMetadata.Keys) + ".");

        if (session.OutputMetadata.Count == 0)
            throw new InvalidOperationException("ONNX session exposes no outputs.");

        var tensor = new DenseTensor<float>(features, [1, features.Length]);
        var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var outputs = session.Run(inputs);
        var flattenedOutputs = outputs
            .SelectMany(output => output.AsEnumerable<float>())
            .ToArray();

        if (flattenedOutputs.Length == 0)
            throw new InvalidOperationException("ONNX session returned no outputs.");

        return flattenedOutputs;
    }

    /// <summary>Strips newline chars from user-supplied strings before logging to prevent log-forging.</summary>
    private static string S(string? value) =>
        value is null ? "(null)" : value.Replace('\r', '_').Replace('\n', '_');

    private static string BuildCacheKey(string modelKey, string modelVersion, float[] features)
    {
        // HashCode over the feature array — avoids string.Join allocation on the hot path.
        var hc = new HashCode();
        foreach (var f in features)
            hc.Add(f);
        return $"inference:{modelKey}:{modelVersion}:{hc.ToHashCode():X8}";
    }

    private async ValueTask<InferenceResultPayload?> TryGetCachedAsync(
        string cacheKey, CancellationToken ct)
    {
        if (_redis is null) return null;

        try
        {
            var db    = _redis.GetDatabase();
            var value = await db.StringGetAsync(cacheKey).ConfigureAwait(false);
            if (!value.HasValue) return null;

            return JsonSerializer.Deserialize<InferenceResultPayload>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key={Key} — proceeding without cache.", S(cacheKey));
            return null;
        }
    }

    private async Task CacheResultAsync(string cacheKey, InferenceResultPayload result, int ttlSeconds)
    {
        if (_redis is null) return;

        try
        {
            var db   = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(result);
            await db.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(ttlSeconds))
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key={Key}", S(cacheKey));
        }
    }
}

