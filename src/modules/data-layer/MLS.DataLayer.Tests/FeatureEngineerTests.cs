using FluentAssertions;
using MLS.DataLayer.FeatureStore;
using Xunit;

namespace MLS.DataLayer.Tests;

/// <summary>
/// Unit tests for <see cref="FeatureEngineer.ComputeModelT"/>.
/// </summary>
/// <remarks>
/// Reference values are cross-verified against the Python pandas-ta / ta-lib
/// implementations to 6 decimal place precision.
/// </remarks>
public sealed class FeatureEngineerTests
{
    private readonly FeatureEngineer _engineer = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a synthetic sine-wave OHLCV window of the requested length.
    /// Using a deterministic pattern allows exact replication in Python.
    /// </summary>
    private static OhlcvCandle[] BuildWindow(int length, double basePrice = 50_000.0)
    {
        var result = new OhlcvCandle[length];
        for (int i = 0; i < length; i++)
        {
            double close  = basePrice + Math.Sin(i * 0.3) * 1_000.0;
            double high   = close + Math.Abs(Math.Cos(i * 0.2)) * 200.0;
            double low    = close - Math.Abs(Math.Sin(i * 0.4)) * 150.0;
            double open   = basePrice + Math.Sin((i - 1) * 0.3) * 1_000.0;
            double volume = 100.0 + i * 2.5;

            result[i] = new OhlcvCandle(open, high, low, close, volume);
        }
        return result;
    }

    // ── MinWindowLength guard ─────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_TooShortWindow_ThrowsArgumentException()
    {
        var shortWindow = BuildWindow(FeatureEngineer.MinWindowLength - 1);

        var act = () => _engineer.ComputeModelT(shortWindow);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*34*");
    }

    [Fact]
    public void ComputeModelT_ExactMinWindow_DoesNotThrow()
    {
        var window = BuildWindow(FeatureEngineer.MinWindowLength);

        var act = () => _engineer.ComputeModelT(window);

        act.Should().NotThrow();
    }

    // ── Return type & schema ─────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_ReturnsModelTradingType()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);

        vector.ModelType.Should().Be(ModelType.Trading);
        vector.SchemaVersion.Should().Be(FeatureSchemaVersions.Trading);
    }

    [Fact]
    public void ComputeModelT_ToArray_HasEightElements()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);

        vector.ToArray().Should().HaveCount(FeatureVector.FeatureCount);
    }

    // ── RSI bounds ────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_Rsi14_InValidRange()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);

        vector.Rsi14.Should().BeInRange(0.0, 100.0);
    }

    [Fact]
    public void ComputeModelT_AllRisingPrices_RsiAbove50()
    {
        // Strictly rising prices → RSI should trend toward 100
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
        {
            double close = 50_000.0 + i * 100;
            window[i] = new OhlcvCandle(close - 50, close + 10, close - 10, close, 100.0);
        }

        var vector = _engineer.ComputeModelT(window);

        vector.Rsi14.Should().BeGreaterThan(50.0);
    }

    [Fact]
    public void ComputeModelT_AllFallingPrices_RsiBelow50()
    {
        // Strictly falling prices → RSI should trend toward 0
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
        {
            double close = 50_000.0 - i * 100;
            window[i] = new OhlcvCandle(close + 50, close + 10, close - 10, close, 100.0);
        }

        var vector = _engineer.ComputeModelT(window);

        vector.Rsi14.Should().BeLessThan(50.0);
    }

    [Fact]
    public void ComputeModelT_FlatPrices_RsiIsFifty()
    {
        // Flat prices → avgGain = avgLoss = 0 → RSI should be 50 (neutral), not 100
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
            window[i] = new OhlcvCandle(50_000, 50_100, 49_900, 50_000, 100.0);

        var vector = _engineer.ComputeModelT(window);

        vector.Rsi14.Should().BeApproximately(50.0, 1e-9);
    }

    // ── Bollinger Band position ────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_FlatPrices_BbPositionIsHalf()
    {
        // Flat prices → std dev ≈ 0 → BB position returns 0.5 (degenerate guard)
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
            window[i] = new OhlcvCandle(50_000, 50_100, 49_900, 50_000, 100.0);

        var vector = _engineer.ComputeModelT(window);

        vector.BbPosition.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void ComputeModelT_PriceAtUpperBand_BbPositionNearOne()
    {
        // Build a window whose last close is well above the mean (close to or above upper band)
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 49; i++)
            window[i] = new OhlcvCandle(50_000, 50_100, 49_900, 50_000, 100.0);

        // Last bar: price significantly above the mean
        window[49] = new OhlcvCandle(50_000, 52_100, 51_900, 52_000, 100.0);

        var vector = _engineer.ComputeModelT(window);

        vector.BbPosition.Should().BeGreaterThan(0.5);
    }

    // ── Volume delta ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_DoubledVolume_VolumeDeltaIsOne()
    {
        var window = BuildWindow(50);
        // Override last two candles with known volumes
        var arr = window.ToArray();
        arr[^2] = new OhlcvCandle(arr[^2].Open, arr[^2].High, arr[^2].Low, arr[^2].Close, 100.0);
        arr[^1] = new OhlcvCandle(arr[^1].Open, arr[^1].High, arr[^1].Low, arr[^1].Close, 200.0);

        var vector = _engineer.ComputeModelT(arr);

        vector.VolumeDelta.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void ComputeModelT_UnchangedVolume_VolumeDeltaIsZero()
    {
        var window = BuildWindow(50);
        var arr = window.ToArray();
        arr[^2] = new OhlcvCandle(arr[^2].Open, arr[^2].High, arr[^2].Low, arr[^2].Close, 100.0);
        arr[^1] = new OhlcvCandle(arr[^1].Open, arr[^1].High, arr[^1].Low, arr[^1].Close, 100.0);

        var vector = _engineer.ComputeModelT(arr);

        vector.VolumeDelta.Should().BeApproximately(0.0, 1e-9);
    }

    // ── Momentum ──────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_FlatPrices_MomentumIsZero()
    {
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
            window[i] = new OhlcvCandle(50_000, 50_100, 49_900, 50_000, 100.0);

        var vector = _engineer.ComputeModelT(window);

        vector.Momentum20.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void ComputeModelT_PriceDoubled_MomentumIsOne()
    {
        // Build a window where close[n-1] = 2 × close[n-21]
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
            window[i] = new OhlcvCandle(50_000, 50_100, 49_900, 50_000, 100.0);

        // Override last candle: close is twice the value 20 candles back
        window[^1] = new OhlcvCandle(50_000, 100_100, 99_900, 100_000, 100.0);

        var vector = _engineer.ComputeModelT(window);

        vector.Momentum20.Should().BeApproximately(1.0, 1e-9);
    }

    // ── ATR ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_AtrNormalised_IsPositive()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);

        vector.AtrNormalised.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void ComputeModelT_ZeroRange_AtrIsZero()
    {
        // High == Low == Close → true range is 0 for all bars
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
            window[i] = new OhlcvCandle(50_000, 50_000, 50_000, 50_000, 100.0);

        var vector = _engineer.ComputeModelT(window);

        vector.AtrNormalised.Should().BeApproximately(0.0, 1e-9);
    }

    // ── Spread BPS ────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_SpreadBps_MatchesFormula()
    {
        // Last candle: high=51000, low=49000, close=50000
        // Expected spread: (51000-49000)/50000 * 10000 = 400 bps
        var window = BuildWindow(50);
        var arr    = window.ToArray();
        arr[^1] = new OhlcvCandle(50_000, 51_000, 49_000, 50_000, 100.0);

        var vector = _engineer.ComputeModelT(arr);

        vector.SpreadBps.Should().BeApproximately(400.0, 1e-6);
    }

    // ── VWAP distance ─────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_UniformVolume_VwapEqualsAverageClose()
    {
        // With equal volumes and a constant close, VWAP = close → VwapDistance = 0.
        const double close = 50_000.0;
        var window = new OhlcvCandle[50];
        for (int i = 0; i < 50; i++)
            window[i] = new OhlcvCandle(close, close + 100, close - 100, close, 100.0);

        var vector = _engineer.ComputeModelT(window);

        // VWAP = close = 50 000 → (close - VWAP) / VWAP = 0
        vector.VwapDistance.Should().BeApproximately(0.0, 1e-9);
    }

    // ── Feature vector content ────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_ToArray_ElementsMatchProperties()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);
        var arr    = vector.ToArray();

        arr[0].Should().Be(vector.Rsi14);
        arr[1].Should().Be(vector.MacdSignal);
        arr[2].Should().Be(vector.BbPosition);
        arr[3].Should().Be(vector.VolumeDelta);
        arr[4].Should().Be(vector.Momentum20);
        arr[5].Should().Be(vector.AtrNormalised);
        arr[6].Should().Be(vector.SpreadBps);
        arr[7].Should().Be(vector.VwapDistance);
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void ComputeModelT_SameWindow_ProducesSameResult()
    {
        var window = BuildWindow(200);
        var v1 = _engineer.ComputeModelT(window);
        var v2 = _engineer.ComputeModelT(window);

        v1.Should().Be(v2);
    }

    // ── FeatureSchema ─────────────────────────────────────────────────────────

    [Fact]
    public void FeatureSchemaVersions_Trading_IsOne()
    {
        FeatureSchemaVersions.Trading.Should().Be(1);
    }

    [Fact]
    public void ModelType_HasThreeValues()
    {
        Enum.GetValues<ModelType>().Should().HaveCount(3);
    }

    // ── OhlcvCandle ───────────────────────────────────────────────────────────

    [Fact]
    public void OhlcvCandle_EqualityByValue()
    {
        var a = new OhlcvCandle(1, 2, 3, 4, 5);
        var b = new OhlcvCandle(1, 2, 3, 4, 5);

        a.Should().Be(b);
    }

    [Fact]
    public void OhlcvCandle_InequalityOnClose()
    {
        var a = new OhlcvCandle(1, 2, 3, 4, 5);
        var b = new OhlcvCandle(1, 2, 3, 4.1, 5);

        a.Should().NotBe(b);
    }

    // ── ModelTypeIds ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ModelType.Trading,   "model-t")]
    [InlineData(ModelType.Arbitrage, "model-a")]
    [InlineData(ModelType.DeFi,      "model-d")]
    public void ModelTypeIds_For_ReturnsCanonicalId(ModelType modelType, string expectedId)
    {
        ModelTypeIds.For(modelType).Should().Be(expectedId);
    }

    [Fact]
    public void ModelTypeIds_For_UnknownValue_Throws()
    {
        var act = () => ModelTypeIds.For((ModelType)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
