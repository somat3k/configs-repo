using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLS.Trader.Configuration;
using MLS.Trader.Models;
using MLS.Trader.Signals;
using Xunit;

namespace MLS.Trader.Tests;

/// <summary>Tests for <see cref="SignalEngine"/>.</summary>
public sealed class SignalEngineTests
{
    private static IOptions<TraderOptions> DefaultOptions(string modelPath = "") =>
        Options.Create(new TraderOptions { ModelPath = modelPath });

    private static MarketFeatures LowRsi(string symbol = "BTC-USDT") =>
        new(symbol, 40_000m, Rsi: 25f, MacdValue: -50f, MacdSignal: -40f,
            BollingerUpper: 42_000f, BollingerMiddle: 40_000f, BollingerLower: 38_000f,
            VolumeDelta: 500_000f, Momentum: -200f, AtrValue: 800f, DateTimeOffset.UtcNow);

    private static MarketFeatures HighRsi(string symbol = "BTC-USDT") =>
        new(symbol, 45_000m, Rsi: 75f, MacdValue: 60f, MacdSignal: 45f,
            BollingerUpper: 47_000f, BollingerMiddle: 45_000f, BollingerLower: 43_000f,
            VolumeDelta: -200_000f, Momentum: 300f, AtrValue: 1_000f, DateTimeOffset.UtcNow);

    private static MarketFeatures NeutralRsi(string symbol = "BTC-USDT") =>
        new(symbol, 42_000m, Rsi: 50f, MacdValue: 10f, MacdSignal: 8f,
            BollingerUpper: 44_000f, BollingerMiddle: 42_000f, BollingerLower: 40_000f,
            VolumeDelta: 100_000f, Momentum: 50f, AtrValue: 600f, DateTimeOffset.UtcNow);

    // ── Rule-based fallback (no ONNX model) ──────────────────────────────────

    [Fact]
    public async Task GenerateSignal_LowRsi_ReturnsBuySignal()
    {
        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var result = await engine.GenerateSignalAsync(LowRsi(), CancellationToken.None);

        result.Direction.Should().Be(SignalDirection.Buy);
        result.Confidence.Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task GenerateSignal_HighRsi_ReturnsSellSignal()
    {
        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var result = await engine.GenerateSignalAsync(HighRsi(), CancellationToken.None);

        result.Direction.Should().Be(SignalDirection.Sell);
        result.Confidence.Should().BeGreaterThan(0f);
    }

    [Fact]
    public async Task GenerateSignal_NeutralRsi_ReturnsHoldSignal()
    {
        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var result = await engine.GenerateSignalAsync(NeutralRsi(), CancellationToken.None);

        result.Direction.Should().Be(SignalDirection.Hold);
    }

    [Fact]
    public async Task GenerateSignal_ConfidenceInValidRange()
    {
        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);

        foreach (var features in new[] { LowRsi(), HighRsi(), NeutralRsi() })
        {
            var result = await engine.GenerateSignalAsync(features, CancellationToken.None);
            result.Confidence.Should().BeInRange(0f, 1f);
        }
    }

    [Fact]
    public async Task GenerateSignal_SetsSymbolFromFeatures()
    {
        var engine   = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var features = LowRsi("ETH-USDT");
        var result   = await engine.GenerateSignalAsync(features, CancellationToken.None);

        result.Symbol.Should().Be("ETH-USDT");
    }

    [Fact]
    public async Task GenerateSignal_SetsTimestamp()
    {
        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var before = DateTimeOffset.UtcNow;
        var result = await engine.GenerateSignalAsync(NeutralRsi(), CancellationToken.None);
        var after  = DateTimeOffset.UtcNow;

        result.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task GenerateSignal_NonexistentModelPath_FallsBackToRules()
    {
        var opts   = DefaultOptions("/nonexistent/model.onnx");
        var engine = new SignalEngine(opts, NullLogger<SignalEngine>.Instance);

        // Should not throw — falls back to rule-based scorer
        var result = await engine.GenerateSignalAsync(LowRsi(), CancellationToken.None);
        result.Direction.Should().Be(SignalDirection.Buy);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var act    = () => engine.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GenerateSignal_BoundaryRsi30_ReturnsBuy()
    {
        var features = new MarketFeatures("SOL-USDT", 100m, Rsi: 29.9f,
            MacdValue: 0f, MacdSignal: 0f, BollingerUpper: 105f, BollingerMiddle: 100f,
            BollingerLower: 95f, VolumeDelta: 0f, Momentum: 0f, AtrValue: 2f, DateTimeOffset.UtcNow);

        var engine = new SignalEngine(DefaultOptions(), NullLogger<SignalEngine>.Instance);
        var result = await engine.GenerateSignalAsync(features, CancellationToken.None);

        result.Direction.Should().Be(SignalDirection.Buy);
    }
}
