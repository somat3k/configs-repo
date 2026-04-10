using Microsoft.ML.OnnxRuntime;

namespace MLS.MLRuntime.Models;

/// <summary>
/// Production <see cref="IInferenceSessionFactory"/> — wraps the real ONNX Runtime constructor.
/// </summary>
internal sealed class DefaultInferenceSessionFactory : IInferenceSessionFactory
{
    /// <inheritdoc/>
    public InferenceSession Create(string modelPath, Microsoft.ML.OnnxRuntime.SessionOptions options) =>
        new(modelPath, options);
}
