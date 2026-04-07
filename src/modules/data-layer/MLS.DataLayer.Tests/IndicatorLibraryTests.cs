using FluentAssertions;
using MLS.DataLayer.FeatureStore;
using Xunit;

namespace MLS.DataLayer.Tests;

/// <summary>
/// Unit tests for <see cref="IndicatorLibrary"/>, <see cref="IndicatorDescriptor"/>,
/// and <see cref="FeatureEngineer.ToPlotSamples"/>.
/// </summary>
public sealed class IndicatorLibraryTests
{
    private readonly FeatureEngineer _engineer = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsNonEmptyList()
    {
        IndicatorLibrary.GetAll().Should().NotBeEmpty();
    }

    [Fact]
    public void GetAll_AllDescriptorsHaveNonEmptyId()
    {
        foreach (var d in IndicatorLibrary.GetAll())
            d.Id.Should().NotBeNullOrWhiteSpace(because: $"{d.Name} must have a non-empty Id");
    }

    [Fact]
    public void GetAll_AllDescriptorsHaveNonEmptyName()
    {
        foreach (var d in IndicatorLibrary.GetAll())
            d.Name.Should().NotBeNullOrWhiteSpace(because: $"Id={d.Id} must have a Name");
    }

    [Fact]
    public void GetAll_AllDescriptorsHaveNonEmptyDescription()
    {
        foreach (var d in IndicatorLibrary.GetAll())
            d.Description.Should().NotBeNullOrWhiteSpace(because: $"Id={d.Id} must have a Description");
    }

    [Fact]
    public void GetAll_AllIdsAreUnique()
    {
        var all = IndicatorLibrary.GetAll();
        all.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase)
           .Should().HaveCount(all.Count, because: "all indicator IDs must be unique");
    }

    [Fact]
    public void GetAll_AllDescriptorsHaveNonNullValueRange()
    {
        foreach (var d in IndicatorLibrary.GetAll())
            d.ValueRange.Should().NotBeNull(because: $"Id={d.Id} must have a ValueRange (use null Min/Max for auto-scale)");
    }

    [Fact]
    public void GetAll_AllDescriptorsHaveNonNullDefaultColor()
    {
        foreach (var d in IndicatorLibrary.GetAll())
            d.DefaultColor.Should().NotBeNullOrWhiteSpace(because: $"Id={d.Id} must have a DefaultColor");
    }

    // ── Model-t feature vector coverage ──────────────────────────────────────

    [Fact]
    public void GetByModelType_Trading_ReturnsExactlyEightIndicators()
    {
        IndicatorLibrary.GetByModelType(ModelType.Trading)
                        .Should().HaveCount(FeatureVector.FeatureCount);
    }

    [Fact]
    public void GetByModelType_Trading_FeatureIndicesAre0Through7()
    {
        var indices = IndicatorLibrary.GetByModelType(ModelType.Trading)
                                      .Select(d => d.FeatureIndex)
                                      .ToList();

        indices.Should().BeEquivalentTo(Enumerable.Range(0, FeatureVector.FeatureCount));
    }

    [Fact]
    public void GetByModelType_Trading_IsOrderedByFeatureIndex()
    {
        var list = IndicatorLibrary.GetByModelType(ModelType.Trading);

        list.Select(d => d.FeatureIndex)
            .Should().BeInAscendingOrder();
    }

    // ── Known model-t descriptor content ─────────────────────────────────────

    [Theory]
    [InlineData(0, "rsi_14")]
    [InlineData(1, "macd_signal")]
    [InlineData(2, "bb_position")]
    [InlineData(3, "volume_delta")]
    [InlineData(4, "momentum_20")]
    [InlineData(5, "atr_norm")]
    [InlineData(6, "spread_bps")]
    [InlineData(7, "vwap_distance")]
    public void GetByFeatureIndex_KnownIndex_ReturnsExpectedId(int featureIndex, string expectedId)
    {
        var descriptor = IndicatorLibrary.GetByFeatureIndex(featureIndex);

        descriptor.Should().NotBeNull();
        descriptor!.Id.Should().Be(expectedId);
    }

    [Fact]
    public void GetByFeatureIndex_NegativeIndex_ReturnsNull()
    {
        IndicatorLibrary.GetByFeatureIndex(-1).Should().BeNull();
    }

    [Fact]
    public void GetByFeatureIndex_OutOfBoundsIndex_ReturnsNull()
    {
        IndicatorLibrary.GetByFeatureIndex(999).Should().BeNull();
    }

    // ── TryGet ────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_KnownId_ReturnsTrueAndPopulatesDescriptor()
    {
        var found = IndicatorLibrary.TryGet("rsi_14", out var desc);

        found.Should().BeTrue();
        desc.Should().NotBeNull();
        desc!.Id.Should().Be("rsi_14");
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        var found = IndicatorLibrary.TryGet("not_an_indicator", out var desc);

        found.Should().BeFalse();
        desc.Should().BeNull();
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        var foundLower = IndicatorLibrary.TryGet("RSI_14",  out var descA);
        var foundUpper = IndicatorLibrary.TryGet("rsi_14",  out var descB);

        foundLower.Should().BeTrue();
        foundUpper.Should().BeTrue();
        descA!.Id.Should().Be(descB!.Id);
    }

    // ── Library-only indicator coverage ──────────────────────────────────────

    [Theory]
    [InlineData("sma_20")]
    [InlineData("ema_9")]
    [InlineData("stoch_rsi_14")]
    [InlineData("cci_20")]
    [InlineData("obv")]
    [InlineData("williams_r_14")]
    public void LibraryOnly_IndicatorsAreRegistered(string id)
    {
        IndicatorLibrary.TryGet(id, out _).Should().BeTrue(because: $"{id} must be in the catalog");
    }

    [Theory]
    [InlineData("sma_20")]
    [InlineData("ema_9")]
    [InlineData("obv")]
    public void LibraryOnly_FeatureIndexIsMinusOne(string id)
    {
        IndicatorLibrary.TryGet(id, out var desc);
        desc!.FeatureIndex.Should().Be(-1);
    }

    [Theory]
    [InlineData("sma_20")]
    [InlineData("ema_9")]
    [InlineData("obv")]
    public void LibraryOnly_ModelTypesIsEmpty(string id)
    {
        IndicatorLibrary.TryGet(id, out var desc);
        desc!.ModelTypes.Should().BeEmpty();
    }

    // ── FeatureEngineer.ToPlotSamples ─────────────────────────────────────────

    [Fact]
    public void ToPlotSamples_CountMatchesFeatureVectorLength()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);
        var ts     = DateTimeOffset.UtcNow;

        var samples = FeatureEngineer.ToPlotSamples(vector, ts);

        samples.Should().HaveCount(FeatureVector.FeatureCount);
    }

    [Fact]
    public void ToPlotSamples_ValuesMatchVectorArray()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);
        var ts     = DateTimeOffset.UtcNow;
        var values = vector.ToArray();

        var samples = FeatureEngineer.ToPlotSamples(vector, ts);

        for (int i = 0; i < values.Length; i++)
            samples[i].Value.Should().Be(values[i], because: $"sample[{i}].Value must match FeatureVector index {i}");
    }

    [Fact]
    public void ToPlotSamples_TimestampConvertsToEpochMs()
    {
        var window = BuildWindow(200);
        var vector = _engineer.ComputeModelT(window);
        var ts     = new DateTimeOffset(2025, 1, 15, 12, 30, 0, TimeSpan.Zero);
        long expectedMs = ts.ToUnixTimeMilliseconds();

        var samples = FeatureEngineer.ToPlotSamples(vector, ts);

        samples.Should().AllSatisfy(s =>
            s.TimestampEpochMs.Should().Be(expectedMs));
    }

    [Fact]
    public void ToPlotSamples_IndicatorIdsMappedFromLibrary()
    {
        var window  = BuildWindow(200);
        var vector  = _engineer.ComputeModelT(window);
        var samples = FeatureEngineer.ToPlotSamples(vector, DateTimeOffset.UtcNow);

        // Every sample's IndicatorId should resolve back to a library descriptor
        foreach (var sample in samples)
        {
            IndicatorLibrary.TryGet(sample.IndicatorId, out var desc).Should().BeTrue(
                because: $"sample IndicatorId '{sample.IndicatorId}' must exist in IndicatorLibrary");
            sample.SeriesName.Should().Be(desc!.Name);
        }
    }

    [Fact]
    public void ToPlotSamples_AllSamplesHaveNonEmptySeriesName()
    {
        var window  = BuildWindow(200);
        var vector  = _engineer.ComputeModelT(window);
        var samples = FeatureEngineer.ToPlotSamples(vector, DateTimeOffset.UtcNow);

        samples.Should().AllSatisfy(s =>
            s.SeriesName.Should().NotBeNullOrWhiteSpace());
    }

    // ── IndicatorPlotSample record equality ───────────────────────────────────

    [Fact]
    public void IndicatorPlotSample_EqualityByValue()
    {
        var a = new IndicatorPlotSample("rsi_14", "RSI (14)", 1_000_000L, 55.3);
        var b = new IndicatorPlotSample("rsi_14", "RSI (14)", 1_000_000L, 55.3);

        a.Should().Be(b);
    }

    [Fact]
    public void IndicatorPlotSample_InequalityOnValue()
    {
        var a = new IndicatorPlotSample("rsi_14", "RSI (14)", 1_000_000L, 55.3);
        var b = new IndicatorPlotSample("rsi_14", "RSI (14)", 1_000_000L, 60.0);

        a.Should().NotBe(b);
    }
}
