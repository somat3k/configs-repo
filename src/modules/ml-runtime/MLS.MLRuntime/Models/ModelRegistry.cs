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
    /// is an internal test-seam type; production code resolves this via a factory lambda in Program.cs.
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

        // Dispose SessionOptions immediately after session creation — the session
        // copies everything it needs during construction and options are no longer required.
        InferenceSession session;
        using (var sessionOptions = BuildSessionOptions())
        {
            session = _sessionFactory.Create(modelPath, sessionOptions);
        }

        var record = new ModelRecord(
            ModelKey:  modelKey,
            ModelPath: modelPath,
            Session:   session,
            LoadedAt:  DateTimeOffset.UtcNow,
            ModelId:   modelId);

        // Hot-reload: swap the dictionary entry FIRST so new requests immediately see the
        // new session, then dispose the old session after a delay that exceeds the inference
        // timeout (default 50 ms) to allow any in-flight calls to complete safely.
        // We intentionally swallow all exceptions in the fire-and-forget continuation — the
        // logger and other DI services may already be in teardown during shutdown.
        ModelRecord? evicted = null;
        _models.AddOrUpdate(modelKey, record, (_, old) => { evicted = old; return record; });

        if (evicted is not null)
        {
            _logger.LogInformation(
                "Hot-reloaded model key={Key} from {Path} (old={OldId} → new={NewId})",
                S(modelKey), S(modelPath), S(evicted.ModelId), S(modelId));

            var oldSession = evicted.Session;
            _ = Task.Delay(TimeSpan.FromMilliseconds(500)).ContinueWith(
                _ => { try { oldSession.Dispose(); } catch { /* intentionally swallowed */ } },
                TaskScheduler.Default);
        }
        else
        {
            _logger.LogInformation(
                "Loading model key={Key} modelId={ModelId} from {Path}",
                S(modelKey), S(modelId), S(modelPath));
        }
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
