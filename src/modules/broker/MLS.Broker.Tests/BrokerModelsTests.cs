using FluentAssertions;
using MLS.Broker.Models;
using Xunit;

namespace MLS.Broker.Tests;

/// <summary>
/// Tests for Broker domain model records and enumerations.
/// </summary>
public sealed class BrokerModelsTests
{
    // ── PlaceOrderRequest ─────────────────────────────────────────────────────

    [Fact]
    public void PlaceOrderRequest_PropertiesRoundtrip()
    {
        var id  = Guid.NewGuid().ToString();
        var req = new PlaceOrderRequest("BTC-USDT", OrderSide.Buy, OrderType.Limit,
            0.5m, 65_000m, null, id, "trader");

        req.Symbol.Should().Be("BTC-USDT");
        req.Side.Should().Be(OrderSide.Buy);
        req.Type.Should().Be(OrderType.Limit);
        req.Quantity.Should().Be(0.5m);
        req.LimitPrice.Should().Be(65_000m);
        req.StopPrice.Should().BeNull();
        req.ClientOrderId.Should().Be(id);
        req.RequestingModuleId.Should().Be("trader");
    }

    [Fact]
    public void PlaceOrderRequest_Equality_SameValues()
    {
        var id = Guid.NewGuid().ToString();
        var a  = new PlaceOrderRequest("ETH-USDT", OrderSide.Sell, OrderType.Market,
            1m, null, null, id, "defi");
        var b  = new PlaceOrderRequest("ETH-USDT", OrderSide.Sell, OrderType.Market,
            1m, null, null, id, "defi");

        a.Should().Be(b);
    }

    // ── OrderResult ───────────────────────────────────────────────────────────

    [Fact]
    public void OrderResult_StateTransition_WithRecord()
    {
        var now    = DateTimeOffset.UtcNow;
        var result = new OrderResult("cloid-1", "venue-42", OrderState.Open,
            0m, null, "hyperliquid", "BTC-USDT", OrderSide.Buy, now, now);

        var filled = result with { State = OrderState.Filled, FilledQuantity = 0.5m, AveragePrice = 65_000m };

        filled.State.Should().Be(OrderState.Filled);
        filled.FilledQuantity.Should().Be(0.5m);
        filled.AveragePrice.Should().Be(65_000m);
        // Original unchanged (immutable record)
        result.State.Should().Be(OrderState.Open);
    }

    [Fact]
    public void OrderResult_Venue_IsHyperliquid()
    {
        var result = new OrderResult("cloid-2", null, OrderState.Pending,
            0m, null, "hyperliquid", "ETH-USDT", OrderSide.Sell,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        result.Venue.Should().Be("hyperliquid");
    }

    // ── OrderSide / OrderType / OrderState ────────────────────────────────────

    [Theory]
    [InlineData(OrderSide.Buy,  "Buy")]
    [InlineData(OrderSide.Sell, "Sell")]
    public void OrderSide_ToStringRoundtrip(OrderSide side, string expected)
        => side.ToString().Should().Be(expected);

    [Theory]
    [InlineData(OrderType.Market,     "Market")]
    [InlineData(OrderType.Limit,      "Limit")]
    [InlineData(OrderType.StopMarket, "StopMarket")]
    [InlineData(OrderType.StopLimit,  "StopLimit")]
    public void OrderType_ToStringRoundtrip(OrderType type, string expected)
        => type.ToString().Should().Be(expected);

    [Theory]
    [InlineData(OrderState.Pending,         "Pending")]
    [InlineData(OrderState.Open,            "Open")]
    [InlineData(OrderState.PartiallyFilled, "PartiallyFilled")]
    [InlineData(OrderState.Filled,          "Filled")]
    [InlineData(OrderState.Cancelled,       "Cancelled")]
    [InlineData(OrderState.Rejected,        "Rejected")]
    public void OrderState_ToStringRoundtrip(OrderState state, string expected)
        => state.ToString().Should().Be(expected);

    // ── FillNotification ──────────────────────────────────────────────────────

    [Fact]
    public void FillNotification_FullFill_RemainingIsZero()
    {
        var fill = new FillNotification("cloid-3", "v-99", "BTC-USDT", OrderSide.Buy,
            0.5m, 65_000m, 0.5m, 0m, "hyperliquid", DateTimeOffset.UtcNow);

        fill.RemainingQuantity.Should().Be(0m);
        fill.TotalFilledQuantity.Should().Be(0.5m);
    }

    [Fact]
    public void FillNotification_PartialFill_RemainingIsPositive()
    {
        var fill = new FillNotification("cloid-4", "v-100", "ETH-USDT", OrderSide.Sell,
            0.25m, 3_500m, 0.25m, 0.75m, "hyperliquid", DateTimeOffset.UtcNow);

        fill.RemainingQuantity.Should().Be(0.75m);
    }

    // ── PositionSnapshot ──────────────────────────────────────────────────────

    [Fact]
    public void PositionSnapshot_LongPosition()
    {
        var pos = new PositionSnapshot("BTC-USDT", OrderSide.Buy, 1m, 60_000m, 5_000m,
            "hyperliquid", DateTimeOffset.UtcNow);

        pos.Side.Should().Be(OrderSide.Buy);
        pos.Quantity.Should().Be(1m);
        pos.UnrealisedPnl.Should().Be(5_000m);
    }

    [Fact]
    public void PositionSnapshot_ShortPosition_SideIsSell()
    {
        var pos = new PositionSnapshot("ETH-USDT", OrderSide.Sell, 2m, 3_400m, -100m,
            "hyperliquid", DateTimeOffset.UtcNow);

        pos.Side.Should().Be(OrderSide.Sell);
        pos.UnrealisedPnl.Should().BeNegative();
    }

    // ── CancelOrderRequest ────────────────────────────────────────────────────

    [Fact]
    public void CancelOrderRequest_PropertiesRoundtrip()
    {
        var req = new CancelOrderRequest("cloid-abc", "trader");
        req.ClientOrderId.Should().Be("cloid-abc");
        req.RequestingModuleId.Should().Be("trader");
    }
}
