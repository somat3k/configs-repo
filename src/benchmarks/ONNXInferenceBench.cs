using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MLS.Benchmarks;

/// <summary>
/// Benchmarks for model-t ONNX inference on the C# hot path using
/// <see cref="Microsoft.ML.OnnxRuntime"/> with a pre-allocated <see cref="DenseTensor{T}"/>
/// that wraps a reused <see cref="float"/> buffer.
/// <para>
/// Performance target: &lt; 10ms p95 for a single 8-feature inference call (CPU EP).
/// </para>
/// <para>
/// The model used here is an embedded minimal Identity ONNX model (float32 [1,8] → [1,8])
/// that exercises the full ONNX Runtime session path without requiring an external file.
/// Replace <see cref="MinimalIdentityModel.ModelBytes"/> with the real model-t artifact
/// (loaded from IPFS / <c>ml-runtime</c>) for accurate production latency numbers.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
public class ONNXInferenceBench
{
    // ── Session + pre-allocated tensors ───────────────────────────────────────

    private InferenceSession _session = null!;

    /// <summary>Pre-allocated input buffer — mutated between calls, never re-allocated.</summary>
    private float[] _inputBuffer = null!;

    /// <summary>Pre-allocated output buffer — reused across every inference call.</summary>
    private float[] _outputBuffer = null!;

    /// <summary>
    /// Pre-allocated <see cref="DenseTensor{T}"/> that wraps <see cref="_inputBuffer"/>.
    /// Because <see cref="DenseTensor{T}"/> accesses the backing array by reference,
    /// mutating <see cref="_inputBuffer"/> between calls is reflected automatically.
    /// </summary>
    private DenseTensor<float> _inputTensor = null!;

    /// <summary>
    /// Pre-allocated inputs list created once in <see cref="Setup"/>.
    /// Reused on every inference call to avoid per-call allocations.
    /// </summary>
    private List<NamedOnnxValue> _inputs = null!;

    // DenseTensor<T> uses int[] dimensions (not long[])
    private static readonly int[] TensorShape = [1, 8];

    // ── Setup / teardown ──────────────────────────────────────────────────────

    [GlobalSetup]
    public void Setup()
    {
        // SessionOptions is IDisposable — dispose with 'using' after session creation
        using var options = new SessionOptions();
        options.AppendExecutionProvider_CPU(0);
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        options.EnableMemoryPattern     = true;

        // ModelBytes is already a byte[] — no extra copy needed
        _session = new InferenceSession(MinimalIdentityModel.ModelBytes, options);

        // Allocate input buffer once; it is mutated between calls, never re-created
        _inputBuffer  = new float[8] { 55.0f, 0.0012f, 0.62f, 0.05f, 0.018f, 0.009f, 23.5f, 0.003f };
        _outputBuffer = new float[8];

        // Pre-allocate tensor and input collection — DenseTensor wraps _inputBuffer by ref
        _inputTensor = new DenseTensor<float>(_inputBuffer, TensorShape);
        _inputs      = [NamedOnnxValue.CreateFromTensor("input", _inputTensor)];
    }

    [GlobalCleanup]
    public void Cleanup() => _session.Dispose();

    // ── Benchmarks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Single inference call reusing pre-allocated <see cref="DenseTensor{T}"/>
    /// and inputs collection — simulates the production inference path.
    /// Target: &lt; 10ms p95.
    /// </summary>
    [Benchmark(Description = "model-t ONNX inference — pre-allocated DenseTensor (reused)")]
    public float[] InferencePreAllocatedTensor()
    {
        using var results = _session.Run(_inputs);

        // Copy output into pre-allocated buffer
        var outputTensor = results[0].AsEnumerable<float>();
        int i = 0;
        foreach (float v in outputTensor)
            _outputBuffer[i++] = v;

        return _outputBuffer;
    }

    /// <summary>
    /// Burst of 10 inference calls reusing the same tensor + inputs collection —
    /// simulates burst inference during a live trading window.
    /// </summary>
    [Benchmark(Description = "model-t ONNX inference — 10-request burst (reused tensor)")]
    public float[] InferenceBurst10()
    {
        for (int call = 0; call < 10; call++)
        {
            // Slightly perturb the RSI feature to prevent JIT constant-folding.
            // Because DenseTensor wraps _inputBuffer by reference, this change is
            // visible to ORT without re-creating the tensor.
            _inputBuffer[0] = 50.0f + call;

            using var results = _session.Run(_inputs);
            var outputTensor = results[0].AsEnumerable<float>();
            int i = 0;
            foreach (float v in outputTensor)
                _outputBuffer[i++] = v;
        }

        return _outputBuffer;
    }
}
