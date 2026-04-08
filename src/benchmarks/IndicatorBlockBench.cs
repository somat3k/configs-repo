using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace MLS.Benchmarks;

/// <summary>
/// Benchmarks for individual vectorisable indicator computations used across
/// the RSI, MACD, Bollinger Bands, and ATR hot paths.
/// <para>
/// Performance targets (L1 — single-thread SIMD-amenable scalar):
/// <list type="bullet">
///   <item>RSI(14) single candle update: &lt; 100ns median (p50)</item>
///   <item>MACD full compute: &lt; 500ns median (p50)</item>
///   <item>BB position (20-bar): &lt; 200ns median (p50)</item>
///   <item>ATR(14) normalised: &lt; 200ns median (p50)</item>
/// </list>
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class IndicatorBlockBench
{
    // ── Constants (mirrored from FeatureEngineer) ─────────────────────────────

    private const int RsiPeriod       = 14;
    private const int MacdFastPeriod  = 12;
    private const int MacdSlowPeriod  = 26;
    private const int MacdSignalPeriod = 9;
    private const int BbPeriod        = 20;
    private const int BbStdDev        = 2;
    private const int AtrPeriod       = 14;

    // ── Candle windows ────────────────────────────────────────────────────────

    // 15 candles: minimum for RSI(14) single-update benchmark
    private double[] _rsiPrices15 = null!;
    // 35 candles: minimum for full MACD (26 slow + 9 signal - 1)
    private double[] _macdPrices35 = null!;
    // 200 candles: realistic live-window size
    private (double High, double Low, double Close, double Volume)[] _candles200 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        // RSI window — 15 close prices with realistic walk
        _rsiPrices15 = BuildPriceWalk(rng, 15, 100.0);

        // MACD window — 35 close prices
        _macdPrices35 = BuildPriceWalk(rng, 35, 50_000.0);

        // Full OHLCV candles for BB, ATR, VWAP
        _candles200 = new (double, double, double, double)[200];
        double price = 50_000.0;
        for (int i = 0; i < 200; i++)
        {
            double open  = price;
            double high  = price * (1 + rng.NextDouble() * 0.005);
            double low   = price * (1 - rng.NextDouble() * 0.005);
            double close = price * (1 + (rng.NextDouble() - 0.5) * 0.004);
            double vol   = 1_000 + rng.NextDouble() * 5_000;
            _candles200[i] = (high, low, close, vol);
            price = close;
        }
    }

    // ── RSI ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// RSI(14) Wilder-smoothed computation over a 15-close window.
    /// Represents a single incremental candle update on the hot path.
    /// Target: &lt; 100ns median.
    /// </summary>
    [Benchmark(Description = "RSI(14) single-candle Wilder update (15 prices)")]
    [BenchmarkCategory("RSI")]
    public double Rsi14SingleCandle() => ComputeRsi(_rsiPrices15, RsiPeriod);

    /// <summary>
    /// RSI(14) over a full 200-price window — tests throughput for batch
    /// feature computation during backfill.
    /// </summary>
    [Benchmark(Description = "RSI(14) full 200-price window")]
    [BenchmarkCategory("RSI")]
    public double Rsi14Full200() => ComputeRsi(
        Array.ConvertAll(_candles200, c => c.Close), RsiPeriod);

    // ── MACD ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// MACD signal line (EMA12, EMA26, signal EMA9) over the minimum 35-price
    /// window. Target: &lt; 500ns median.
    /// </summary>
    [Benchmark(Description = "MACD full compute (35 prices, fast=12 slow=26 sig=9)")]
    [BenchmarkCategory("MACD")]
    public double MacdMinWindow() => ComputeMacdSignal(_macdPrices35);

    /// <summary>
    /// MACD signal line over a full 200-price window.
    /// </summary>
    [Benchmark(Description = "MACD full compute (200 prices)")]
    [BenchmarkCategory("MACD")]
    public double MacdFull200() => ComputeMacdSignal(
        Array.ConvertAll(_candles200, c => c.Close));

    // ── Bollinger Bands ───────────────────────────────────────────────────────

    /// <summary>
    /// Bollinger Band position (20-bar, 2σ) over a 200-candle window.
    /// Target: &lt; 200ns median.
    /// </summary>
    [Benchmark(Description = "Bollinger Band position BB(20,2) — 200 candles")]
    [BenchmarkCategory("BB")]
    public double BbPosition200() => ComputeBollingerPosition(
        Array.ConvertAll(_candles200, c => c.Close), BbPeriod, BbStdDev);

    // ── ATR ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// ATR(14) Wilder-smoothed, normalised by close — over 200 OHLC candles.
    /// This is the full-window (batch) computation; useful for backfill.
    /// Note: full-window ATR is O(N) and expected to exceed the 200ns target.
    /// </summary>
    [Benchmark(Description = "ATR(14) normalised — 200 OHLC candles (full window, batch)")]
    [BenchmarkCategory("ATR")]
    public double AtrNormalised200() => ComputeAtrNormalised(_candles200, AtrPeriod);

    /// <summary>
    /// ATR(14) incremental single-candle Wilder update — one new candle appended
    /// to an existing smoothed ATR value.  This is the live-trading hot path.
    /// Target: &lt; 200ns median.
    /// </summary>
    [Benchmark(Description = "ATR(14) incremental single-candle Wilder update (TARGET < 200ns)")]
    [BenchmarkCategory("ATR")]
    public double AtrIncrementalUpdate()
    {
        // Simulate the live update: previousAtr is already computed, apply Wilder smoothing
        // to the new candle's true range only.
        const double previousAtr = 125.5;
        const int    period      = AtrPeriod;
        var last = _candles200[199];
        var prev = _candles200[198];

        double tr  = Tr(last, prev.Close);
        double atr = (previousAtr * (period - 1) + tr) / period;

        double lastClose = last.Close;
        return lastClose < double.Epsilon ? 0.0 : atr / lastClose;
    }

    // ── Private implementations ───────────────────────────────────────────────
    // These are inlined copies of the private methods in FeatureEngineer,
    // isolated here so each indicator can be benchmarked independently.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeRsi(double[] prices, int period)
    {
        int n = prices.Length;
        double avgGain = 0.0;
        double avgLoss = 0.0;

        int seedEnd = Math.Min(period, n - 1);
        for (int i = 1; i <= seedEnd; i++)
        {
            double delta = prices[i] - prices[i - 1];
            if (delta > 0) avgGain += delta;
            else           avgLoss -= delta;
        }

        avgGain /= period;
        avgLoss /= period;

        for (int i = period + 1; i < n; i++)
        {
            double delta = prices[i] - prices[i - 1];
            double gain  = delta > 0 ?  delta : 0.0;
            double loss  = delta < 0 ? -delta : 0.0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }

        if (avgLoss < double.Epsilon && avgGain < double.Epsilon) return 50.0;
        if (avgLoss < double.Epsilon) return 100.0;
        if (avgGain < double.Epsilon) return 0.0;

        double rs = avgGain / avgLoss;
        return 100.0 - 100.0 / (1.0 + rs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeMacdSignal(double[] prices)
    {
        int n = prices.Length;
        double kFast = 2.0 / (MacdFastPeriod + 1);
        double kSlow = 2.0 / (MacdSlowPeriod + 1);
        double kSig  = 2.0 / (MacdSignalPeriod + 1);

        double ema12     = prices[0];
        double ema26     = prices[0];
        int    macdStart = MacdSlowPeriod - 1;
        double signalEma = 0.0;
        int    sigCount  = 0;

        for (int i = 1; i < n; i++)
        {
            double close = prices[i];
            ema12 = close * kFast + ema12 * (1 - kFast);
            ema26 = close * kSlow + ema26 * (1 - kSlow);

            if (i < macdStart) continue;

            double macdLine = ema12 - ema26;
            if (sigCount == 0) signalEma = macdLine;
            else               signalEma = macdLine * kSig + signalEma * (1 - kSig);
            sigCount++;
        }

        double lastClose = prices[n - 1];
        return Math.Abs(lastClose) < double.Epsilon ? 0.0 : signalEma / lastClose;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeBollingerPosition(double[] closes, int period, int mult)
    {
        int n     = closes.Length;
        int start = Math.Max(0, n - period);
        int cnt   = n - start;

        double sum = 0.0;
        for (int i = start; i < n; i++) sum += closes[i];
        double mean = sum / cnt;

        double variance = 0.0;
        for (int i = start; i < n; i++)
        {
            double diff = closes[i] - mean;
            variance += diff * diff;
        }

        double stdDev = Math.Sqrt(variance / cnt);
        double upper  = mean + mult * stdDev;
        double lower  = mean - mult * stdDev;
        double range  = upper - lower;

        return range < double.Epsilon ? 0.5 : (closes[n - 1] - lower) / range;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeAtrNormalised(
        (double High, double Low, double Close, double Volume)[] candles, int period)
    {
        int n = candles.Length;
        if (n < 2) return 0.0;

        double atr = Tr(candles[1], candles[0].Close);
        for (int i = 2; i <= Math.Min(period, n - 1); i++)
            atr += Tr(candles[i], candles[i - 1].Close);
        atr /= Math.Min(period, n - 1);

        for (int i = period + 1; i < n; i++)
        {
            double tr = Tr(candles[i], candles[i - 1].Close);
            atr = (atr * (period - 1) + tr) / period;
        }

        double lastClose = candles[n - 1].Close;
        return lastClose < double.Epsilon ? 0.0 : atr / lastClose;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Tr((double High, double Low, double Close, double Volume) c, double prevClose)
    {
        double hl  = c.High - c.Low;
        double hpc = Math.Abs(c.High - prevClose);
        double lpc = Math.Abs(c.Low  - prevClose);
        return Math.Max(hl, Math.Max(hpc, lpc));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double[] BuildPriceWalk(Random rng, int length, double start)
    {
        double[] prices = new double[length];
        prices[0] = start;
        for (int i = 1; i < length; i++)
            prices[i] = prices[i - 1] * (1 + (rng.NextDouble() - 0.5) * 0.01);
        return prices;
    }
}
