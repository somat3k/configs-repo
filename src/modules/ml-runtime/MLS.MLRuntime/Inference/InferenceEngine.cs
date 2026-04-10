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
/// </summary>
public sealed class InferenceEngine(
    IModelRegistry _registry,
    IConnectionMultiplexer? _redis,
    IOptions<MLRuntimeOptions> _options,
    ILogger<InferenceEngine> _logger) : IInferenceEngine
{
    /// <inheritdoc/>
    public async ValueTask<InferenceResultPayload> RunAsync(
        InferenceRequestPayload request, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(request.ModelKey, request.Features);

        // 1. Try Redis cache
        var cached = await TryGetCachedAsync(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null)
            return cached with { RequestId = request.RequestId, Cached = true };

        // 2. Resolve session
        var record = await _registry.GetAsync(request.ModelKey, ct).ConfigureAwait(false);
        if (record is null)
            throw new InvalidOperationException($"Model not loaded: {request.ModelKey}");

        // 3. Run inference with timeout
        var sw  = Stopwatch.StartNew();
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

        // 4. Cache result (fire-and-forget)
        _ = CacheResultAsync(cacheKey, result, _options.Value.RedisCacheTtlSeconds);

        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static float[] RunOnnx(InferenceSession session, float[] features)
    {
        var tensor = new DenseTensor<float>(features, [1, features.Length]);
        var inputs = new[] { NamedOnnxValue.CreateFromTensor("input", tensor) };

        using var outputs = session.Run(inputs);
        var first = outputs.FirstOrDefault()
            ?? throw new InvalidOperationException("ONNX session returned no outputs.");

        return first.AsEnumerable<float>().ToArray();
    }

    /// <summary>Strips newline chars from user-supplied strings before logging to prevent log-forging.</summary>
    private static string S(string? value) =>
        value is null ? "(null)" : value.Replace('\r', '_').Replace('\n', '_');

    private static string BuildCacheKey(string modelKey, float[] features)
    {
        // HashCode over the feature array — avoids string.Join allocation on the hot path.
        var hc = new HashCode();
        foreach (var f in features)
            hc.Add(f);
        return $"inference:{modelKey}:{hc.ToHashCode():X8}";
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
