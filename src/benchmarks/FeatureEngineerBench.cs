using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using MLS.DataLayer.FeatureStore;

namespace MLS.Benchmarks;

/// <summary>
/// Benchmarks for the <see cref="FeatureEngineer.ComputeModelT"/> hot path —
/// the 8-feature vector computation from a raw OHLCV candle window.
/// <para>
/// Performance target: &lt; 1ms for a 200-candle window (p50 median).
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
public class FeatureEngineerBench
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private FeatureEngineer _engineer = null!;

    private OhlcvCandle[] _window34  = null!;   // minimum window
    private OhlcvCandle[] _window100 = null!;
    private OhlcvCandle[] _window200 = null!;   // target production window

    // Fixed timestamp captured once in Setup so the ToPlotSamples benchmark
    // measures only projection/allocation cost, not DateTimeOffset.UtcNow overhead.
    private DateTimeOffset _fixedTimestamp;

    // ── Setup ─────────────────────────────────────────────────────────────────

    [GlobalSetup]
    public void Setup()
    {
        _engineer = new FeatureEngineer();

        var rng = new Random(42);
        _window34  = BuildCandles(rng, FeatureEngineer.MinWindowLength, 50_000.0);
        _window100 = BuildCandles(rng, 100, 50_000.0);
        _window200 = BuildCandles(rng, 200, 50_000.0);

        _fixedTimestamp = DateTimeOffset.UtcNow;
    }

    // ── Benchmarks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Feature vector with the minimum 34-candle window.
    /// Stress-tests the worst-case (shortest) look-back.
    /// </summary>
    [Benchmark(Description = "Feature vector — 34-candle minimum window")]
    public FeatureVector ComputeMinWindow() =>
        _engineer.ComputeModelT(_window34);

    /// <summary>
    /// Feature vector with a 100-candle window.
    /// </summary>
    [Benchmark(Description = "Feature vector — 100-candle window")]
    public FeatureVector Compute100() =>
        _engineer.ComputeModelT(_window100);

    /// <summary>
    /// Feature vector with the standard 200-candle production window.
    /// Performance target: &lt; 1ms p50.
    /// </summary>
    [Benchmark(Description = "Feature vector — 200-candle production window (TARGET < 1ms)")]
    public FeatureVector Compute200() =>
        _engineer.ComputeModelT(_window200);

    /// <summary>
    /// Converts a computed feature vector to chart-ready plot samples.
    /// Exercises the <see cref="FeatureEngineer.ToPlotSamples"/> allocation path.
    /// Returns the full sample list to prevent the JIT from eliding the call.
    /// Uses a fixed timestamp captured in <see cref="Setup"/> to avoid
    /// <see cref="DateTimeOffset.UtcNow"/> overhead skewing results.
    /// </summary>
    [Benchmark(Description = "ToPlotSamples — project FeatureVector to 8 IndicatorPlotSamples")]
    public IReadOnlyList<IndicatorPlotSample> ToPlotSamples()
    {
        var vector = _engineer.ComputeModelT(_window200);
        return FeatureEngineer.ToPlotSamples(vector, _fixedTimestamp);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OhlcvCandle[] BuildCandles(Random rng, int count, double startPrice)
    {
        var candles = new OhlcvCandle[count];
        double price = startPrice;

        for (int i = 0; i < count; i++)
        {
            double open  = price;
            double high  = price * (1 + rng.NextDouble() * 0.006);
            double low   = price * (1 - rng.NextDouble() * 0.006);
            double close = price * (1 + (rng.NextDouble() - 0.5) * 0.004);
            double vol   = 500 + rng.NextDouble() * 4_500;

            candles[i] = new OhlcvCandle(open, high, low, close, vol);
            price = close;
        }

        return candles;
    }
}
