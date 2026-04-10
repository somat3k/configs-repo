using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLS.Trader.Configuration;
using MLS.Trader.Models;
using MLS.Trader.Risk;
using Xunit;

namespace MLS.Trader.Tests;

/// <summary>Tests for <see cref="RiskManager"/>.</summary>
public sealed class RiskManagerTests
{
    private static IOptions<TraderOptions> OptionsOf(
        decimal maxPositionUsd  = 10_000m,
        double  riskRewardRatio = 2.0,
        double  atrMultiplier   = 2.0,
        double  stopLossPct     = 0.02,
        decimal accountEquity   = 100_000m) =>
        Options.Create(new TraderOptions
        {
            MaxPositionSizeUsd = maxPositionUsd,
            RiskRewardRatio    = riskRewardRatio,
            AtrMultiplier      = atrMultiplier,
            StopLossPercent    = stopLossPct,
            AccountEquityUsd   = accountEquity,
        });

    private static RiskManager Create(IOptions<TraderOptions>? opts = null) =>
        new(opts ?? OptionsOf(), NullLogger<RiskManager>.Instance);

    // ── ComputePositionSize ───────────────────────────────────────────────────

    [Fact]
    public void ComputePositionSize_HighConfidence_ReturnsPositiveSize()
    {
        var mgr  = Create(OptionsOf(accountEquity: 100_000m, maxPositionUsd: 50_000m));
        var size = mgr.ComputePositionSize(0.8f, 2.0);

        size.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void ComputePositionSize_CappedAtMaxPositionSizeUsd()
    {
        // Kelly fraction with 90% confidence, 2:1 R:R on a large account
        var mgr  = Create(OptionsOf(maxPositionUsd: 1_000m, accountEquity: 1_000_000m));
        var size = mgr.ComputePositionSize(0.9f, 2.0);

        size.Should().BeLessThanOrEqualTo(1_000m);
    }

    [Fact]
    public void ComputePositionSize_ZeroConfidence_ReturnsZero()
    {
        var mgr  = Create();
        var size = mgr.ComputePositionSize(0f, 2.0);

        // Kelly fraction: 0 - 1/2 = -0.5 → clamped to 0
        size.Should().Be(0m);
    }

    [Fact]
    public void ComputePositionSize_NegativeKellyFraction_ReturnsZero()
    {
        // confidence < 1/(1+b): p=0.2, b=2 → Kelly = 0.2 - 0.8/2 = -0.2 ≤ 0
        var mgr  = Create();
        var size = mgr.ComputePositionSize(0.2f, 2.0);

        size.Should().Be(0m);
    }

    [Fact]
    public void ComputePositionSize_KellyFractionComputedCorrectly()
    {
        // p=0.7, b=2, q=0.3 → Kelly = 0.7 - 0.3/2 = 0.55
        // Position = 0.55 * 10000 = 5500
        var mgr    = Create(OptionsOf(accountEquity: 10_000m, maxPositionUsd: 50_000m));
        var size   = mgr.ComputePositionSize(0.7f, 2.0);

        size.Should().BeApproximately(5_500m, 50m);
    }

    // ── ComputeStopLoss ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeStopLoss_AtrBased_Long_SubtractsAtrMultiplier()
    {
        var mgr        = Create(OptionsOf(atrMultiplier: 2.0));
        var entry      = 40_000m;
        var atr        = 500f;
        var stopLoss   = mgr.ComputeStopLoss(entry, SignalDirection.Buy, atr);

        // Expected: 40000 - (500 * 2) = 39000
        stopLoss.Should().Be(39_000m);
    }

    [Fact]
    public void ComputeStopLoss_AtrBased_Short_AddsAtrMultiplier()
    {
        var mgr      = Create(OptionsOf(atrMultiplier: 2.0));
        var entry    = 40_000m;
        var atr      = 500f;
        var stopLoss = mgr.ComputeStopLoss(entry, SignalDirection.Sell, atr);

        // Expected: 40000 + (500 * 2) = 41000
        stopLoss.Should().Be(41_000m);
    }

    [Fact]
    public void ComputeStopLoss_ZeroAtr_UsesFixedPercent_Long()
    {
        var mgr      = Create(OptionsOf(stopLossPct: 0.02));
        var entry    = 50_000m;
        var stopLoss = mgr.ComputeStopLoss(entry, SignalDirection.Buy, atr: 0f);

        // Expected: 50000 - (50000 * 0.02) = 49000
        stopLoss.Should().Be(49_000m);
    }

    [Fact]
    public void ComputeStopLoss_ZeroAtr_UsesFixedPercent_Short()
    {
        var mgr      = Create(OptionsOf(stopLossPct: 0.02));
        var entry    = 50_000m;
        var stopLoss = mgr.ComputeStopLoss(entry, SignalDirection.Sell, atr: 0f);

        // Expected: 50000 + (50000 * 0.02) = 51000
        stopLoss.Should().Be(51_000m);
    }

    [Fact]
    public void ComputeStopLoss_StopLossBelowEntryForLong()
    {
        var mgr      = Create();
        var entry    = 30_000m;
        var stopLoss = mgr.ComputeStopLoss(entry, SignalDirection.Buy, 400f);

        stopLoss.Should().BeLessThan(entry);
    }

    [Fact]
    public void ComputeStopLoss_StopLossAboveEntryForShort()
    {
        var mgr      = Create();
        var entry    = 30_000m;
        var stopLoss = mgr.ComputeStopLoss(entry, SignalDirection.Sell, 400f);

        stopLoss.Should().BeGreaterThan(entry);
    }

    // ── ComputeTakeProfit ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeTakeProfit_TwoToOneRatio_Long()
    {
        var mgr        = Create(OptionsOf(riskRewardRatio: 2.0));
        var entry      = 40_000m;
        var stopLoss   = 39_000m; // 1000 distance
        var takeProfit = mgr.ComputeTakeProfit(entry, stopLoss, SignalDirection.Buy);

        // Expected: 40000 + 1000*2 = 42000
        takeProfit.Should().Be(42_000m);
    }

    [Fact]
    public void ComputeTakeProfit_TwoToOneRatio_Short()
    {
        var mgr        = Create(OptionsOf(riskRewardRatio: 2.0));
        var entry      = 40_000m;
        var stopLoss   = 41_000m; // 1000 distance
        var takeProfit = mgr.ComputeTakeProfit(entry, stopLoss, SignalDirection.Sell);

        // Expected: 40000 - 1000*2 = 38000
        takeProfit.Should().Be(38_000m);
    }

    [Fact]
    public void ComputeTakeProfit_ThreeToOneRatio_Long()
    {
        var mgr        = Create(OptionsOf(riskRewardRatio: 3.0));
        var entry      = 100m;
        var stopLoss   = 98m;  // 2 distance
        var takeProfit = mgr.ComputeTakeProfit(entry, stopLoss, SignalDirection.Buy);

        // Expected: 100 + 2*3 = 106
        takeProfit.Should().Be(106m);
    }
}
