namespace MLS.MLRuntime.Models;

/// <summary>
/// Registry of in-process ONNX models, keyed by model identifier (<c>model-t</c>,
/// <c>model-a</c>, <c>model-d</c>). Supports hot-reload: loading a key that already
/// exists disposes the old session and swaps in the new one atomically.
/// </summary>
public interface IModelRegistry : IDisposable
{
    /// <summary>Snapshot of all currently loaded models.</summary>
    IReadOnlyDictionary<string, ModelRecord> Loaded { get; }

    /// <summary>Returns the <see cref="ModelRecord"/> for the given key, or <see langword="null"/>.</summary>
    ValueTask<ModelRecord?> GetAsync(string modelKey, CancellationToken ct = default);

    /// <summary>
    /// Loads (or hot-reloads) an ONNX model from <paramref name="modelPath"/>.
    /// Any existing session for <paramref name="modelKey"/> is disposed first.
    /// </summary>
    Task LoadAsync(string modelKey, string modelPath, string? modelId = null, CancellationToken ct = default);

    /// <summary>Unloads and disposes the session for <paramref name="modelKey"/>.</summary>
    Task UnloadAsync(string modelKey, CancellationToken ct = default);
}
