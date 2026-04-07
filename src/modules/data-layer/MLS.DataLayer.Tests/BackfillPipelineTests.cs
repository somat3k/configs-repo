using System.Text.Json;
using FluentAssertions;
using MLS.DataLayer.Hydra;
using MLS.DataLayer.Persistence;
using Xunit;

namespace MLS.DataLayer.Tests;

/// <summary>
/// Tests for <see cref="BackfillPipeline"/> gap-range handling and
/// Hyperliquid candle parsing (object form and array form).
/// </summary>
public sealed class BackfillPipelineTests
{
    // ── GapRange record ───────────────────────────────────────────────────────

    [Fact]
    public void GapRange_HoldsMissingCandleCount()
    {
        var key  = new FeedKey("hyperliquid", "BTC-USDT", "1h");
        var from = DateTimeOffset.UtcNow.AddHours(-3);
        var to   = DateTimeOffset.UtcNow;
        var gap  = new GapRange(key, from, to, 3);

        gap.MissingCandles.Should().Be(3);
        gap.GapStart.Should().Be(from);
        gap.GapEnd.Should().Be(to);
    }

    // ── ParseHyperliquidCandles (via reflection-exposed internal method) ───────
    // We test the parsing indirectly by invoking the static helper through
    // a JsonDocument constructed to match both known response shapes.

    [Fact]
    public void ParseHyperliquidCandles_ObjectForm_ParsesOHLCV()
    {
        // Arrange — object-form candleSnapshot response
        var json = """
            [
              {"t":1714000000000,"T":1714000059999,"s":"BTC","i":"1h",
               "o":"65000","h":"65200","l":"64900","c":"65100","v":"12.3","n":150}
            ]
            """;
        using var doc  = JsonDocument.Parse(json);
        var key        = new FeedKey("hyperliquid", "BTC-USDT", "1h");
        var candles    = InvokeParseHyperliquidCandles(doc.RootElement, key, 100);

        candles.Should().HaveCount(1);
        candles[0].Open.Should().BeApproximately(65000, 0.001);
        candles[0].High.Should().BeApproximately(65200, 0.001);
        candles[0].Low.Should().BeApproximately(64900, 0.001);
        candles[0].Close.Should().BeApproximately(65100, 0.001);
        candles[0].Volume.Should().BeApproximately(12.3, 0.001);
        candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1714000000000L));
    }

    [Fact]
    public void ParseHyperliquidCandles_ArrayForm_ParsesOHLCV()
    {
        // Arrange — positional array form: [t, T, s, i, o, c, h, l, v, n]
        var json = """
            [
              [1714000000000,1714000059999,"BTC","1h","65000","65100","65200","64900","12.3",150]
            ]
            """;
        using var doc = JsonDocument.Parse(json);
        var key       = new FeedKey("hyperliquid", "BTC-USDT", "1h");
        var candles   = InvokeParseHyperliquidCandles(doc.RootElement, key, 100);

        candles.Should().HaveCount(1);
        candles[0].Open.Should().BeApproximately(65000, 0.001);
        candles[0].Close.Should().BeApproximately(65100, 0.001);
        candles[0].High.Should().BeApproximately(65200, 0.001);
        candles[0].Low.Should().BeApproximately(64900, 0.001);
        candles[0].Volume.Should().BeApproximately(12.3, 0.001);
    }

    [Fact]
    public void ParseHyperliquidCandles_LimitApplied_TruncatesResult()
    {
        var items  = Enumerable.Range(0, 10).Select(i =>
            $"{{\"t\":{1714000000000L + i * 3_600_000},\"o\":\"1\",\"h\":\"1\",\"l\":\"1\",\"c\":\"1\",\"v\":\"1\"}}");
        var json   = $"[{string.Join(",", items)}]";
        using var doc = JsonDocument.Parse(json);
        var candles   = InvokeParseHyperliquidCandles(doc.RootElement, new FeedKey("hyperliquid", "BTC-USDT", "1h"), 3);

        candles.Should().HaveCount(3);
    }

    [Fact]
    public void ParseHyperliquidCandles_EmptyArray_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("[]");
        var candles   = InvokeParseHyperliquidCandles(doc.RootElement, new FeedKey("hyperliquid", "X", "1h"), 100);
        candles.Should().BeEmpty();
    }

    [Fact]
    public void ParseHyperliquidCandles_NonArrayRoot_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("{\"error\":\"bad\"}");
        var candles   = InvokeParseHyperliquidCandles(doc.RootElement, new FeedKey("hyperliquid", "X", "1h"), 100);
        candles.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Invokes the private static ParseHyperliquidCandles via reflection so we can
    /// test the parsing logic without a full HTTP stack.
    /// </summary>
    private static IReadOnlyList<CandleEntity> InvokeParseHyperliquidCandles(
        JsonElement root, FeedKey key, int limit)
    {
        var method = typeof(BackfillPipeline).GetMethod(
            "ParseHyperliquidCandles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        return (IReadOnlyList<CandleEntity>)method.Invoke(null, [root, key, limit])!;
    }
}
