using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLS.Trader.Models;
using MLS.Trader.Orders;
using MLS.Trader.Persistence;
using MLS.Trader.Services;
using Xunit;

namespace MLS.Trader.Tests;

/// <summary>Tests for <see cref="OrderManager"/>.</summary>
public sealed class OrderManagerTests
{
    private readonly DbContextOptions<TraderDbContext> _dbOpts;
    private readonly Mock<IDbContextFactory<TraderDbContext>> _factoryMock;
    private readonly Mock<IEnvelopeSender> _senderMock;

    public OrderManagerTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _dbOpts = new DbContextOptionsBuilder<TraderDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _factoryMock = new Mock<IDbContextFactory<TraderDbContext>>();
        _factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TraderDbContext(_dbOpts));

        _senderMock = new Mock<IEnvelopeSender>();
        _senderMock
            .Setup(s => s.SendEnvelopeAsync(It.IsAny<MLS.Core.Contracts.EnvelopePayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private OrderManager Create() =>
        new(_factoryMock.Object, _senderMock.Object,
            NullLogger<OrderManager>.Instance);

    // ── CreateOrderAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_PersistsToDatabase()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync(
            "BTC-USDT", SignalDirection.Buy, 0.1m, 40_000m, 39_000m, 42_000m,
            paperTrading: false, CancellationToken.None);

        await using var db = new TraderDbContext(_dbOpts);
        var found = await db.Orders.FirstOrDefaultAsync(o => o.ClientOrderId == order.ClientOrderId);
        found.Should().NotBeNull();
        found!.Symbol.Should().Be("BTC-USDT");
    }

    [Fact]
    public async Task CreateOrder_LiveMode_StateIsPending()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync(
            "ETH-USDT", SignalDirection.Sell, 1m, 2_500m, 2_600m, 2_300m,
            paperTrading: false, CancellationToken.None);

        order.State.Should().Be(TraderOrderState.Pending);
    }

    [Fact]
    public async Task CreateOrder_PaperTrading_StateIsFilled()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync(
            "SOL-USDT", SignalDirection.Buy, 5m, 100m, 96m, 108m,
            paperTrading: true, CancellationToken.None);

        order.State.Should().Be(TraderOrderState.Filled);
    }

    [Fact]
    public async Task CreateOrder_LiveMode_SendsOrderCreateEnvelope()
    {
        var mgr = Create();
        await mgr.CreateOrderAsync(
            "BTC-USDT", SignalDirection.Buy, 0.5m, 40_000m, 39_000m, 42_000m,
            paperTrading: false, CancellationToken.None);

        _senderMock.Verify(
            s => s.SendEnvelopeAsync(
                It.Is<MLS.Core.Contracts.EnvelopePayload>(e => e.Type == "ORDER_CREATE"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrder_PaperTrading_DoesNotSendEnvelope()
    {
        var mgr = Create();
        await mgr.CreateOrderAsync(
            "BTC-USDT", SignalDirection.Buy, 0.1m, 40_000m, 39_000m, 42_000m,
            paperTrading: true, CancellationToken.None);

        _senderMock.Verify(
            s => s.SendEnvelopeAsync(It.IsAny<MLS.Core.Contracts.EnvelopePayload>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrder_GeneratesUniqueClientOrderId()
    {
        var mgr = Create();
        var o1  = await mgr.CreateOrderAsync("BTC-USDT", SignalDirection.Buy, 0.1m, 40_000m, 39_000m, 42_000m, true, CancellationToken.None);
        var o2  = await mgr.CreateOrderAsync("BTC-USDT", SignalDirection.Buy, 0.1m, 40_000m, 39_000m, 42_000m, true, CancellationToken.None);

        o1.ClientOrderId.Should().NotBe(o2.ClientOrderId);
    }

    // ── GetOrderAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrder_ReturnsNullWhenNotFound()
    {
        var mgr    = Create();
        var result = await mgr.GetOrderAsync(Guid.NewGuid().ToString(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrder_ReturnsCachedOrder()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync("BTC-USDT", SignalDirection.Sell, 0.2m, 40_000m, 41_000m, 38_000m, true, CancellationToken.None);

        var fetched = await mgr.GetOrderAsync(order.ClientOrderId, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.ClientOrderId.Should().Be(order.ClientOrderId);
    }

    // ── CancelOrderAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_UpdatesStateToCancelled()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync("BTC-USDT", SignalDirection.Buy, 0.1m, 40_000m, 39_000m, 42_000m, true, CancellationToken.None);

        await mgr.CancelOrderAsync(order.ClientOrderId, CancellationToken.None);

        var fetched = await mgr.GetOrderAsync(order.ClientOrderId, CancellationToken.None);
        fetched!.State.Should().Be(TraderOrderState.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_LiveOrder_SendsOrderCancelEnvelope()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync("BTC-USDT", SignalDirection.Buy, 0.1m, 40_000m, 39_000m, 42_000m, paperTrading: false, CancellationToken.None);

        _senderMock.Invocations.Clear();

        await mgr.CancelOrderAsync(order.ClientOrderId, CancellationToken.None);

        _senderMock.Verify(
            s => s.SendEnvelopeAsync(
                It.Is<MLS.Core.Contracts.EnvelopePayload>(e => e.Type == "ORDER_CANCEL"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── UpdateOrderStateAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrderState_TransitionsStateCorrectly()
    {
        var mgr   = Create();
        var order = await mgr.CreateOrderAsync("ETH-USDT", SignalDirection.Buy, 1m, 2_500m, 2_400m, 2_700m, true, CancellationToken.None);

        await mgr.UpdateOrderStateAsync(order.ClientOrderId, TraderOrderState.Open, CancellationToken.None);

        var fetched = await mgr.GetOrderAsync(order.ClientOrderId, CancellationToken.None);
        fetched!.State.Should().Be(TraderOrderState.Open);
    }
}
