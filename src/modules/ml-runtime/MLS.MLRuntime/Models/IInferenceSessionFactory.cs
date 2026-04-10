using Microsoft.ML.OnnxRuntime;

namespace MLS.MLRuntime.Models;

/// <summary>
/// Internal factory abstraction for creating <see cref="InferenceSession"/> instances.
/// Allows unit tests to inject a stub without touching the file system.
/// </summary>
internal interface IInferenceSessionFactory
{
    /// <summary>Creates an <see cref="InferenceSession"/> from the specified ONNX model file.</summary>
    InferenceSession Create(string modelPath, Microsoft.ML.OnnxRuntime.SessionOptions options);
}
