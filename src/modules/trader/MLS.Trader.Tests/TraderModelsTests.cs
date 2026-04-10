using FluentAssertions;
using MLS.Trader.Models;
using Xunit;

namespace MLS.Trader.Tests;

/// <summary>Tests for Trader module model types.</summary>
public sealed class TraderModelsTests
{
    // ── SignalDirection ───────────────────────────────────────────────────────

    [Fact]
    public void SignalDirection_HasThreeValues()
    {
        var values = Enum.GetValues<SignalDirection>();
        values.Should().HaveCount(3);
        values.Should().Contain(SignalDirection.Buy);
        values.Should().Contain(SignalDirection.Sell);
        values.Should().Contain(SignalDirection.Hold);
    }

    // ── TraderOrderState ──────────────────────────────────────────────────────

    [Fact]
    public void TraderOrderState_HasSixValues()
    {
        var values = Enum.GetValues<TraderOrderState>();
        values.Should().HaveCount(6);
    }

    [Fact]
    public void TraderOrderState_ContainsExpectedStates()
    {
        var values = Enum.GetValues<TraderOrderState>();
        values.Should().Contain(TraderOrderState.Draft);
        values.Should().Contain(TraderOrderState.Pending);
        values.Should().Contain(TraderOrderState.Open);
        values.Should().Contain(TraderOrderState.PartiallyFilled);
        values.Should().Contain(TraderOrderState.Filled);
        values.Should().Contain(TraderOrderState.Cancelled);
    }

    // ── TradeSignalResult ─────────────────────────────────────────────────────

    [Fact]
    public void TradeSignalResult_ConstructsCorrectly()
    {
        var ts     = DateTimeOffset.UtcNow;
        var result = new TradeSignalResult("BTC-USDT", SignalDirection.Buy, 0.85f, ts);

        result.Symbol.Should().Be("BTC-USDT");
        result.Direction.Should().Be(SignalDirection.Buy);
        result.Confidence.Should().BeApproximately(0.85f, 0.001f);
        result.Timestamp.Should().Be(ts);
    }

    // ── MarketFeatures ────────────────────────────────────────────────────────

    [Fact]
    public void MarketFeatures_ConstructsCorrectly()
    {
        var ts       = DateTimeOffset.UtcNow;
        var features = new MarketFeatures(
            "ETH-USDT", 2_500m, 45f, 10f, 8f,
            2_600f, 2_500f, 2_400f, 500_000f, 20f, 80f, ts);

        features.Symbol.Should().Be("ETH-USDT");
        features.Price.Should().Be(2_500m);
        features.Rsi.Should().Be(45f);
        features.AtrValue.Should().Be(80f);
    }

    // ── TraderOrder ───────────────────────────────────────────────────────────

    [Fact]
    public void TraderOrder_RecordEquality()
    {
        var now = DateTimeOffset.UtcNow;
        var o1  = new TraderOrder("id-1", "BTC-USDT", SignalDirection.Buy,
                       0.5m, 40_000m, 39_000m, 42_000m, TraderOrderState.Pending, false, now, now);
        var o2  = new TraderOrder("id-1", "BTC-USDT", SignalDirection.Buy,
                       0.5m, 40_000m, 39_000m, 42_000m, TraderOrderState.Pending, false, now, now);

        o1.Should().Be(o2);
    }

    [Fact]
    public void TraderOrder_WithExpression_UpdatesState()
    {
        var now = DateTimeOffset.UtcNow;
        var o1  = new TraderOrder("id-1", "BTC-USDT", SignalDirection.Buy,
                       0.5m, 40_000m, 39_000m, 42_000m, TraderOrderState.Pending, false, now, now);

        var o2 = o1 with { State = TraderOrderState.Open };

        o2.State.Should().Be(TraderOrderState.Open);
        o2.ClientOrderId.Should().Be(o1.ClientOrderId);
    }

    // ── RiskParameters ────────────────────────────────────────────────────────

    [Fact]
    public void RiskParameters_ConstructsCorrectly()
    {
        var rp = new RiskParameters(5_000m, 38_000m, 42_000m);

        rp.PositionSizeUsd.Should().Be(5_000m);
        rp.StopLossPrice.Should().Be(38_000m);
        rp.TakeProfitPrice.Should().Be(42_000m);
    }

    // ── TraderPosition ────────────────────────────────────────────────────────

    [Fact]
    public void TraderPosition_ConstructsCorrectly()
    {
        var pos = new TraderPosition("BTC-USDT", SignalDirection.Buy,
            0.5m, 40_000m, 250m, "hyperliquid", DateTimeOffset.UtcNow);

        pos.Symbol.Should().Be("BTC-USDT");
        pos.Quantity.Should().Be(0.5m);
        pos.UnrealisedPnl.Should().Be(250m);
    }
}
