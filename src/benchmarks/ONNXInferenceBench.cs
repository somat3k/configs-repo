using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MLS.Benchmarks;

/// <summary>
/// Benchmarks for model-t ONNX inference on the C# hot path using
/// <see cref="Microsoft.ML.OnnxRuntime"/> with pre-allocated <see cref="OrtValue"/> tensors.
/// <para>
/// Performance target: &lt; 10ms p95 for a single 8-feature inference call (CPU EP).
/// </para>
/// <para>
/// The model used here is an embedded minimal Identity ONNX model (float32 [1,8] → [1,8])
/// that exercises the full ONNX Runtime session path without requiring an external file.
/// Replace <see cref="MinimalIdentityModel.Bytes"/> with the real model-t artifact
/// (loaded from IPFS / <c>ml-runtime</c>) for accurate production latency numbers.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
public class ONNXInferenceBench
{
    // ── Session + pre-allocated tensors ───────────────────────────────────────

    private InferenceSession _session = null!;

    /// <summary>Pre-allocated input buffer — reused across every inference call.</summary>
    private float[] _inputBuffer = null!;

    /// <summary>Pre-allocated output buffer — reused across every inference call.</summary>
    private float[] _outputBuffer = null!;

    // DenseTensor<T> uses int[] dimensions (not long[])
    private static readonly int[] TensorShape = [1, 8];

    // ── Setup / teardown ──────────────────────────────────────────────────────

    [GlobalSetup]
    public void Setup()
    {
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU(0);
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        options.EnableMemoryPattern     = true;

        // InferenceSession requires a byte[] (not ReadOnlySpan<byte>)
        _session = new InferenceSession(MinimalIdentityModel.Bytes.ToArray(), options);

        // Pre-allocate tensors once — zero allocation on the hot path
        _inputBuffer  = new float[8] { 55.0f, 0.0012f, 0.62f, 0.05f, 0.018f, 0.009f, 23.5f, 0.003f };
        _outputBuffer = new float[8];
    }

    [GlobalCleanup]
    public void Cleanup() => _session.Dispose();

    // ── Benchmarks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Single inference call using pre-allocated DenseTensor — simulates the
    /// production inference path with reused memory.
    /// Target: &lt; 10ms p95.
    /// </summary>
    [Benchmark(Description = "model-t ONNX inference — pre-allocated DenseTensor")]
    public float[] InferencePreAllocatedTensor()
    {
        // Reuse the pre-allocated input tensor each call — zero allocation
        var inputTensor = new DenseTensor<float>(_inputBuffer, TensorShape);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
        };

        using var results = _session.Run(inputs);

        // Copy output into pre-allocated buffer — no heap allocation on the result path
        var outputTensor = results[0].AsEnumerable<float>();
        int i = 0;
        foreach (float v in outputTensor)
            _outputBuffer[i++] = v;

        return _outputBuffer;
    }

    /// <summary>
    /// Batch inference: 10 requests in a tight loop — simulates burst inference
    /// during a live trading window.
    /// </summary>
    [Benchmark(Description = "model-t ONNX inference — 10-request burst")]
    public float[] InferenceBurst10()
    {
        float[] lastOutput = _outputBuffer;
        for (int call = 0; call < 10; call++)
        {
            // Slightly perturb the RSI feature to prevent JIT constant-folding
            _inputBuffer[0] = 50.0f + call;

            var inputTensor = new DenseTensor<float>(_inputBuffer, TensorShape);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor),
            };

            using var results = _session.Run(inputs);
            var outputTensor = results[0].AsEnumerable<float>();
            int i = 0;
            foreach (float v in outputTensor)
                _outputBuffer[i++] = v;

            lastOutput = _outputBuffer;
        }
        return lastOutput;
    }
}
