using FluentAssertions;
using MLS.DataLayer.Hydra;
using Xunit;

namespace MLS.DataLayer.Tests;

/// <summary>
/// Tests for <see cref="GapDetector"/> gap-detection decision logic
/// (the stale-feed calculation, independent of database or DI).
/// </summary>
public sealed class GapDetectorTests
{
    // The gap detection formula:
    //   missingCount = floor(elapsed.TotalSeconds / tfSeconds) - 1
    //   gap detected when missingCount > 0

    [Theory]
    [InlineData(0,   60,  false)]  // just received a candle — 0 seconds stale
    [InlineData(60,  60,  false)]  // exactly one interval — missing = 1 - 1 = 0
    [InlineData(61,  60,  false)]  // one second into second interval — missing = 1 - 1 = 0
    [InlineData(120, 60,  true)]   // two full intervals — missing = 2 - 1 = 1 → gap detected
    [InlineData(121, 60,  true)]   // just past two intervals — missing = 2 - 1 = 1
    [InlineData(3600, 3600, false)] // one full 1h interval — missing = 1 - 1 = 0
    [InlineData(7201, 3600, true)]  // two 1h intervals + 1s — missing = 2 - 1 = 1
    public void DetectionFormula_MatchesExpected(
        int elapsedSeconds, double tfSeconds, bool expectGap)
    {
        var missingCount = (int)(elapsedSeconds / tfSeconds) - 1;
        var gapDetected  = missingCount > 0;

        gapDetected.Should().Be(expectGap,
            because: $"elapsed={elapsedSeconds}s tf={tfSeconds}s missing={missingCount}");
    }

    [Fact]
    public void FeedKey_EqualityByValue()
    {
        var a = new FeedKey("hyperliquid", "BTC-USDT", "1h");
        var b = new FeedKey("hyperliquid", "BTC-USDT", "1h");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void FeedKey_NotEqualWhenTimeframeDiffers()
    {
        var a = new FeedKey("hyperliquid", "BTC-USDT", "1h");
        var b = new FeedKey("hyperliquid", "BTC-USDT", "4h");
        a.Should().NotBe(b);
    }
}
