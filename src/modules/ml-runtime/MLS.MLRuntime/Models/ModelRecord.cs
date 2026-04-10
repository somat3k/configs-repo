using Microsoft.ML.OnnxRuntime;

namespace MLS.MLRuntime.Models;

/// <summary>
/// Represents a loaded ONNX model in the <see cref="ModelRegistry"/>.
/// </summary>
/// <param name="ModelKey">Registry key — <c>model-t</c>, <c>model-a</c>, or <c>model-d</c>.</param>
/// <param name="ModelPath">Container-local path from which the session was loaded.</param>
/// <param name="Session">The live ONNX <see cref="InferenceSession"/>.</param>
/// <param name="LoadedAt">UTC timestamp of when this session was loaded.</param>
/// <param name="ModelId">Optional versioned identifier, e.g. <c>model-t-v3.1</c>.</param>
public sealed record ModelRecord(
    string ModelKey,
    string ModelPath,
    InferenceSession Session,
    DateTimeOffset LoadedAt,
    string? ModelId = null);
