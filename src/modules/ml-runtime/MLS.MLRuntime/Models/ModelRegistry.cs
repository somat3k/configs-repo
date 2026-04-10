using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;

namespace MLS.MLRuntime.Models;

/// <summary>
/// Thread-safe in-process registry of loaded ONNX <see cref="InferenceSession"/> instances.
/// </summary>
public sealed class ModelRegistry : IModelRegistry
{
    private readonly IInferenceSessionFactory _sessionFactory;
    private readonly ILogger<ModelRegistry> _logger;
    private readonly ConcurrentDictionary<string, ModelRecord> _models =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Initialises a new <see cref="ModelRegistry"/>.
    /// The constructor is <see langword="internal"/> because <see cref="IInferenceSessionFactory"/>
    /// is an internal test-seam type; production code resolves this via the DI container.
    /// </summary>
    internal ModelRegistry(IInferenceSessionFactory sessionFactory, ILogger<ModelRegistry> logger)
    {
        _sessionFactory = sessionFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ModelRecord> Loaded => _models;

    /// <inheritdoc/>
    public ValueTask<ModelRecord?> GetAsync(string modelKey, CancellationToken ct = default)
    {
        _models.TryGetValue(modelKey, out var record);
        return ValueTask.FromResult(record);
    }

    /// <inheritdoc/>
    public Task LoadAsync(string modelKey, string modelPath, string? modelId = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"ONNX model file not found: {modelPath}", modelPath);

        var sessionOptions = BuildSessionOptions();
        var session = _sessionFactory.Create(modelPath, sessionOptions);

        var record = new ModelRecord(
            ModelKey:  modelKey,
            ModelPath: modelPath,
            Session:   session,
            LoadedAt:  DateTimeOffset.UtcNow,
            ModelId:   modelId);

        // Replace and dispose any existing session (hot-reload)
        if (_models.TryGetValue(modelKey, out var existing))
        {
            _logger.LogInformation(
                "Hot-reloading model key={Key} from {Path} (replacing {OldId})",
                S(modelKey), S(modelPath), S(existing.ModelId));
            existing.Session.Dispose();
        }
        else
        {
            _logger.LogInformation(
                "Loading model key={Key} modelId={ModelId} from {Path}",
                S(modelKey), S(modelId), S(modelPath));
        }

        _models[modelKey] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnloadAsync(string modelKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_models.TryRemove(modelKey, out var record))
        {
            record.Session.Dispose();
            _logger.LogInformation("Unloaded model key={Key}", S(modelKey));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var record in _models.Values)
        {
            try { record.Session.Dispose(); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing session for key={Key}", record.ModelKey);
            }
        }

        _models.Clear();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>Strips newline characters from user-supplied strings before logging to prevent log-forging.</summary>
    private static string S(string? value) =>
        value is null ? "(null)" : value.Replace('\r', '_').Replace('\n', '_');

    private static Microsoft.ML.OnnxRuntime.SessionOptions BuildSessionOptions() =>
        new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode          = ExecutionMode.ORT_PARALLEL,
            InterOpNumThreads      = 2,
            IntraOpNumThreads      = 2,
        };
}
