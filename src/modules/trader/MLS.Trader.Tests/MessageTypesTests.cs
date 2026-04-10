using FluentAssertions;
using MLS.Core.Constants;
using Xunit;

namespace MLS.Trader.Tests;

/// <summary>Verifies that all message type constants used by the Trader module are present in <see cref="MessageTypes"/>.</summary>
public sealed class MessageTypesTests
{
    [Fact]
    public void TradeSignal_ConstantIsCorrectValue()
        => MessageTypes.TradeSignal.Should().Be("TRADE_SIGNAL");

    [Fact]
    public void OrderCreate_ConstantIsCorrectValue()
        => MessageTypes.OrderCreate.Should().Be("ORDER_CREATE");

    [Fact]
    public void OrderCancel_ConstantIsCorrectValue()
        => MessageTypes.OrderCancel.Should().Be("ORDER_CANCEL");

    [Fact]
    public void MarketDataUpdate_ConstantIsCorrectValue()
        => MessageTypes.MarketDataUpdate.Should().Be("MARKET_DATA_UPDATE");

    [Fact]
    public void InferenceResult_ConstantIsCorrectValue()
        => MessageTypes.InferenceResult.Should().Be("INFERENCE_RESULT");

    [Fact]
    public void PositionUpdate_ConstantIsCorrectValue()
        => MessageTypes.PositionUpdate.Should().Be("POSITION_UPDATE");

    [Fact]
    public void ModuleHeartbeat_ConstantIsCorrectValue()
        => MessageTypes.ModuleHeartbeat.Should().Be("MODULE_HEARTBEAT");

    [Fact]
    public void OrderConfirmation_ConstantIsCorrectValue()
        => MessageTypes.OrderConfirmation.Should().Be("ORDER_CONFIRMATION");

    [Fact]
    public void FillNotification_ConstantIsCorrectValue()
        => MessageTypes.FillNotification.Should().Be("FILL_NOTIFICATION");
}
