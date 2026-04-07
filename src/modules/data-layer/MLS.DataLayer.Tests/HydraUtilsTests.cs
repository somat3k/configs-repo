using System.Text.Json;
using FluentAssertions;
using MLS.DataLayer.Hydra;
using Xunit;

namespace MLS.DataLayer.Tests;

/// <summary>
/// Tests for <see cref="HydraUtils"/> parsing helpers and sanitisation logic.
/// </summary>
public sealed class HydraUtilsTests
{
    // ── SanitiseFeedId ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("hyperliquid", "hyperliquid")]
    [InlineData("BTC-USDT", "BTC-USDT")]
    [InlineData("1h", "1h")]
    [InlineData("bad\nid", "bad_id")]
    [InlineData("bad\rid", "bad_id")]
    [InlineData("<script>", "_script_")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitiseFeedId_ReturnsExpected(string? input, string expected)
    {
        HydraUtils.SanitiseFeedId(input).Should().Be(expected);
    }

    [Fact]
    public void SanitiseFeedId_TruncatesAt64Chars()
    {
        var input    = new string('a', 100);
        var sanitised = HydraUtils.SanitiseFeedId(input);
        sanitised.Length.Should().Be(64);
    }

    // ── SanitisePeerId ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("module-123", "module-123")]
    [InlineData("a\rb", "a_b")]
    [InlineData("a\nb", "a_b")]
    public void SanitisePeerId_ReplacesControlChars(string input, string expected)
    {
        HydraUtils.SanitisePeerId(input).Should().Be(expected);
    }

    [Fact]
    public void SanitisePeerId_TruncatesAt64Chars()
    {
        var input = new string('x', 100);
        HydraUtils.SanitisePeerId(input).Length.Should().Be(64);
    }

    // ── ParseJsonDouble ───────────────────────────────────────────────────────

    [Fact]
    public void ParseJsonDouble_ReturnsValueForJsonNumber()
    {
        using var doc = JsonDocument.Parse("65000.5");
        HydraUtils.ParseJsonDouble(doc.RootElement).Should().Be(65000.5);
    }

    [Fact]
    public void ParseJsonDouble_ReturnsValueForJsonString()
    {
        using var doc = JsonDocument.Parse("\"65000.5\"");
        HydraUtils.ParseJsonDouble(doc.RootElement).Should().Be(65000.5);
    }

    [Fact]
    public void ParseJsonDouble_ReturnsZeroForNull()
    {
        using var doc = JsonDocument.Parse("null");
        HydraUtils.ParseJsonDouble(doc.RootElement).Should().Be(0.0);
    }

    [Fact]
    public void ParseJsonDouble_ReturnsZeroForUnparsableString()
    {
        using var doc = JsonDocument.Parse("\"not-a-number\"");
        HydraUtils.ParseJsonDouble(doc.RootElement).Should().Be(0.0);
    }

    // ── GetJsonDouble ─────────────────────────────────────────────────────────

    [Fact]
    public void GetJsonDouble_ReturnsPropertyValue()
    {
        using var doc = JsonDocument.Parse("{\"o\":\"65000\"}");
        HydraUtils.GetJsonDouble(doc.RootElement, "o").Should().Be(65000.0);
    }

    [Fact]
    public void GetJsonDouble_ReturnsZeroForMissingProperty()
    {
        using var doc = JsonDocument.Parse("{}");
        HydraUtils.GetJsonDouble(doc.RootElement, "missing").Should().Be(0.0);
    }

    // ── TimeframeToSeconds ────────────────────────────────────────────────────

    [Theory]
    [InlineData("1m",  60)]
    [InlineData("5m",  300)]
    [InlineData("15m", 900)]
    [InlineData("1h",  3600)]
    [InlineData("4h",  14400)]
    [InlineData("1d",  86400)]
    [InlineData("1w",  604800)]
    public void TimeframeToSeconds_ReturnsCorrectValues(string tf, double expected)
    {
        HydraUtils.TimeframeToSeconds(tf).Should().Be(expected);
    }

    // ── DeriveHyperliquidCoin ─────────────────────────────────────────────────

    [Theory]
    [InlineData("BTC-USDT", "BTC")]
    [InlineData("WBTC-USDT", "BTC")]
    [InlineData("WETH-USD", "ETH")]
    [InlineData("ETH/USDT", "ETH")]
    public void DeriveHyperliquidCoin_StripsWrappedPrefix(string symbol, string expected)
    {
        HydraUtils.DeriveHyperliquidCoin(symbol).Should().Be(expected);
    }
}
