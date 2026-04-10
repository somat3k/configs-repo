using FluentAssertions;
using MLS.DeFi.Models;
using Xunit;

namespace MLS.DeFi.Tests;

/// <summary>
/// Tests for DeFi domain model records and enumerations.
/// </summary>
public sealed class DeFiModelsTests
{
    // ── DeFiOrderRequest ──────────────────────────────────────────────────────

    [Fact]
    public void DeFiOrderRequest_PropertiesRoundtrip()
    {
        var id  = Guid.NewGuid().ToString();
        var req = new DeFiOrderRequest("BTC-USDT", DeFiOrderSide.Buy, DeFiOrderType.Limit,
            0.5m, 65_000m, null, id, "trader");

        req.Symbol.Should().Be("BTC-USDT");
        req.Side.Should().Be(DeFiOrderSide.Buy);
        req.Type.Should().Be(DeFiOrderType.Limit);
        req.Quantity.Should().Be(0.5m);
        req.LimitPrice.Should().Be(65_000m);
        req.StopPrice.Should().BeNull();
        req.ClientOrderId.Should().Be(id);
        req.RequestingModuleId.Should().Be("trader");
    }

    [Fact]
    public void DeFiOrderRequest_Equality_SameValues()
    {
        var id = Guid.NewGuid().ToString();
        var a  = new DeFiOrderRequest("ETH-USDT", DeFiOrderSide.Sell, DeFiOrderType.Market,
            1m, null, null, id, "defi");
        var b  = new DeFiOrderRequest("ETH-USDT", DeFiOrderSide.Sell, DeFiOrderType.Market,
            1m, null, null, id, "defi");

        a.Should().Be(b);
    }

    // ── DeFiOrderResult ───────────────────────────────────────────────────────

    [Fact]
    public void DeFiOrderResult_StateTransition_WithRecord()
    {
        var now    = DateTimeOffset.UtcNow;
        var result = new DeFiOrderResult("cloid-1", "v-42", DeFiOrderState.Open,
            0m, null, "hyperliquid", "BTC-USDT", DeFiOrderSide.Buy, now, now);

        var filled = result with { State = DeFiOrderState.Filled, FilledQuantity = 0.5m, AveragePrice = 65_000m };

        filled.State.Should().Be(DeFiOrderState.Filled);
        filled.FilledQuantity.Should().Be(0.5m);
        filled.AveragePrice.Should().Be(65_000m);
        result.State.Should().Be(DeFiOrderState.Open);
    }

    [Fact]
    public void DeFiOrderResult_Venue_IsHyperliquid()
    {
        var result = new DeFiOrderResult("cloid-2", null, DeFiOrderState.Pending,
            0m, null, "hyperliquid", "ETH-USDT", DeFiOrderSide.Sell,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        result.Venue.Should().Be("hyperliquid");
    }

    // ── DeFiOrderSide / DeFiOrderType / DeFiOrderState ────────────────────────

    [Theory]
    [InlineData(DeFiOrderSide.Buy,  "Buy")]
    [InlineData(DeFiOrderSide.Sell, "Sell")]
    public void DeFiOrderSide_ToStringRoundtrip(DeFiOrderSide side, string expected)
        => side.ToString().Should().Be(expected);

    [Theory]
    [InlineData(DeFiOrderType.Market,     "Market")]
    [InlineData(DeFiOrderType.Limit,      "Limit")]
    [InlineData(DeFiOrderType.StopMarket, "StopMarket")]
    [InlineData(DeFiOrderType.StopLimit,  "StopLimit")]
    public void DeFiOrderType_ToStringRoundtrip(DeFiOrderType type, string expected)
        => type.ToString().Should().Be(expected);

    [Theory]
    [InlineData(DeFiOrderState.Pending,         "Pending")]
    [InlineData(DeFiOrderState.Open,            "Open")]
    [InlineData(DeFiOrderState.PartiallyFilled, "PartiallyFilled")]
    [InlineData(DeFiOrderState.Filled,          "Filled")]
    [InlineData(DeFiOrderState.Cancelled,       "Cancelled")]
    [InlineData(DeFiOrderState.Rejected,        "Rejected")]
    public void DeFiOrderState_ToStringRoundtrip(DeFiOrderState state, string expected)
        => state.ToString().Should().Be(expected);

    // ── DeFiFillNotification ──────────────────────────────────────────────────

    [Fact]
    public void DeFiFillNotification_FullFill_RemainingIsZero()
    {
        var fill = new DeFiFillNotification("cloid-3", "v-99", "BTC-USDT", DeFiOrderSide.Buy,
            0.5m, 65_000m, 0.5m, 0m, "hyperliquid", DateTimeOffset.UtcNow);

        fill.RemainingQuantity.Should().Be(0m);
        fill.TotalFilledQuantity.Should().Be(0.5m);
    }

    [Fact]
    public void DeFiFillNotification_PartialFill_RemainingIsPositive()
    {
        var fill = new DeFiFillNotification("cloid-4", "v-100", "ETH-USDT", DeFiOrderSide.Sell,
            0.25m, 3_500m, 0.25m, 0.75m, "hyperliquid", DateTimeOffset.UtcNow);

        fill.RemainingQuantity.Should().Be(0.75m);
    }

    [Fact]
    public void DeFiFillNotification_UnknownRemaining_IsSentinelMinusOne()
    {
        var fill = new DeFiFillNotification("cloid-5", "v-101", "BTC-USDT", DeFiOrderSide.Buy,
            0.1m, 60_000m, 0.1m, -1m, "hyperliquid", DateTimeOffset.UtcNow);

        fill.RemainingQuantity.Should().Be(-1m);
    }

    // ── DeFiPositionSnapshot ──────────────────────────────────────────────────

    [Fact]
    public void DeFiPositionSnapshot_LongPosition()
    {
        var pos = new DeFiPositionSnapshot("BTC-USDT", DeFiOrderSide.Buy, 1m, 60_000m, 5_000m,
            "hyperliquid", DateTimeOffset.UtcNow);

        pos.Side.Should().Be(DeFiOrderSide.Buy);
        pos.Quantity.Should().Be(1m);
        pos.UnrealisedPnl.Should().Be(5_000m);
    }

    [Fact]
    public void DeFiPositionSnapshot_ShortPosition_SideIsSell()
    {
        var pos = new DeFiPositionSnapshot("ETH-USDT", DeFiOrderSide.Sell, 2m, 3_400m, -100m,
            "hyperliquid", DateTimeOffset.UtcNow);

        pos.Side.Should().Be(DeFiOrderSide.Sell);
        pos.UnrealisedPnl.Should().BeNegative();
    }

    // ── OnChainTransactionRequest ─────────────────────────────────────────────

    [Fact]
    public void OnChainTransactionRequest_PropertiesRoundtrip()
    {
        var req = new OnChainTransactionRequest("camelot-router", "aabbcc", 0m, 200_000, "defi");
        req.AddressName.Should().Be("camelot-router");
        req.EncodedCalldata.Should().Be("aabbcc");
        req.GasLimit.Should().Be(200_000);
        req.RequestingModuleId.Should().Be("defi");
    }

    // ── OnChainTransactionResult ──────────────────────────────────────────────

    [Fact]
    public void OnChainTransactionResult_PendingState()
    {
        var result = new OnChainTransactionResult("0xabc123", OnChainTxStatus.Pending, 0, 0, DateTimeOffset.UtcNow);
        result.Status.Should().Be(OnChainTxStatus.Pending);
        result.TxHash.Should().Be("0xabc123");
    }

    [Fact]
    public void OnChainTransactionResult_Confirmed()
    {
        var result = new OnChainTransactionResult("0xdef456", OnChainTxStatus.Confirmed, 21_000, 19_000_000, DateTimeOffset.UtcNow);
        result.Status.Should().Be(OnChainTxStatus.Confirmed);
        result.GasUsed.Should().Be(21_000);
        result.BlockNumber.Should().Be(19_000_000);
    }

    // ── DeFiStrategyRequest / DeFiStrategyResult ──────────────────────────────

    [Fact]
    public void DeFiStrategyRequest_PropertiesRoundtrip()
    {
        var req = new DeFiStrategyRequest("BTC-USDT", DeFiOrderSide.Buy, 1m, 50, "defi");
        req.Symbol.Should().Be("BTC-USDT");
        req.MaxSlippageBps.Should().Be(50);
        req.RequestingModuleId.Should().Be("defi");
    }

    [Fact]
    public void DeFiStrategyResult_HyperliquidPerpetual_DefaultVenue()
    {
        var req    = new DeFiStrategyRequest("BTC-USDT", DeFiOrderSide.Buy, 1m, 50, "defi");
        var order  = new DeFiOrderRequest(req.Symbol, req.Side, DeFiOrderType.Market,
            req.Quantity, null, null, Guid.NewGuid().ToString(), req.RequestingModuleId);
        var result = new DeFiStrategyResult(DeFiStrategyType.HyperliquidPerpetual,
            "hyperliquid", 0.9995m, 2, order);

        result.StrategyType.Should().Be(DeFiStrategyType.HyperliquidPerpetual);
        result.Venue.Should().Be("hyperliquid");
        result.EstimatedFeesBps.Should().Be(2);
    }

    // ── WalletSignResult ──────────────────────────────────────────────────────

    [Fact]
    public void WalletSignResult_PropertiesRoundtrip()
    {
        var sr = new WalletSignResult("0xsignedhex", "0xwalletaddress");
        sr.SignedTxHex.Should().Be("0xsignedhex");
        sr.Address.Should().Be("0xwalletaddress");
    }

    // ── DeFiStrategyType enumeration ──────────────────────────────────────────

    [Theory]
    [InlineData(DeFiStrategyType.HyperliquidPerpetual, "HyperliquidPerpetual")]
    [InlineData(DeFiStrategyType.CamelotSpotSwap,      "CamelotSpotSwap")]
    [InlineData(DeFiStrategyType.DfynSpotSwap,         "DfynSpotSwap")]
    [InlineData(DeFiStrategyType.BalancerPool,         "BalancerPool")]
    [InlineData(DeFiStrategyType.MorphoLending,        "MorphoLending")]
    [InlineData(DeFiStrategyType.NHopRoute,            "NHopRoute")]
    public void DeFiStrategyType_ToStringRoundtrip(DeFiStrategyType type, string expected)
        => type.ToString().Should().Be(expected);

    // ── OnChainTxStatus enumeration ───────────────────────────────────────────

    [Theory]
    [InlineData(OnChainTxStatus.Pending,   "Pending")]
    [InlineData(OnChainTxStatus.Confirmed, "Confirmed")]
    [InlineData(OnChainTxStatus.Reverted,  "Reverted")]
    [InlineData(OnChainTxStatus.Failed,    "Failed")]
    public void OnChainTxStatus_ToStringRoundtrip(OnChainTxStatus status, string expected)
        => status.ToString().Should().Be(expected);
}
