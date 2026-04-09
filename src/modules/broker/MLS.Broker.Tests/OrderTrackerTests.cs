using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;
using MLS.Broker.Persistence;
using MLS.Broker.Services;
using StackExchange.Redis;
using Xunit;

namespace MLS.Broker.Tests;

/// <summary>
/// Tests for <see cref="OrderTracker"/> using an in-memory EF Core database
/// and a mocked Redis connection.
/// </summary>
public sealed class OrderTrackerTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly DbContextOptions<BrokerDbContext> _dbOpts;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly OrderTracker _tracker;

    public OrderTrackerTests()
    {
        _dbOpts = new DbContextOptionsBuilder<BrokerDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        // Factory creates a fresh context per call (same in-memory DB via name)
        var factoryMock = new Mock<IDbContextFactory<BrokerDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(() => new BrokerDbContext(_dbOpts));

        // Redis mock — stub string get/set
        _dbMock    = new Mock<IDatabase>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                  .Returns(_dbMock.Object);

        _dbMock.Setup(d => d.StringSetAsync(
                   It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                   It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
                   It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(true);

        _dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ReturnsAsync(RedisValue.Null);

        _tracker = new OrderTracker(factoryMock.Object, _redisMock.Object,
            NullLogger<OrderTracker>.Instance);
    }

    // ── TrackAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TrackAsync_PersistsOrderToDatabase()
    {
        var order = SampleOrder("cloid-t1");
        await _tracker.TrackAsync(order, CancellationToken.None);

        await using var db = new BrokerDbContext(_dbOpts);
        var entity = await db.Orders.FirstOrDefaultAsync(o => o.ClientOrderId == "cloid-t1");
        entity.Should().NotBeNull();
    }

    [Fact]
    public async Task TrackAsync_WritesToRedisCache()
    {
        var order = SampleOrder("cloid-t2");
        await _tracker.TrackAsync(order, CancellationToken.None);

        _dbMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => ((string)k!).Contains("cloid-t2")),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesDatabaseState()
    {
        var order = SampleOrder("cloid-t3");
        await _tracker.TrackAsync(order, CancellationToken.None);

        await _tracker.UpdateAsync("cloid-t3", OrderState.Filled, 0.5m, 65_000m, CancellationToken.None);

        await using var db = new BrokerDbContext(_dbOpts);
        var entity = await db.Orders.FirstOrDefaultAsync(o => o.ClientOrderId == "cloid-t3");
        entity!.State.Should().Be("Filled");
        entity.FilledQuantity.Should().Be(0.5m);
        entity.AveragePrice.Should().Be(65_000m);
    }

    // ── GetOpenOrdersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOpenOrdersAsync_YieldsOpenAndPartialOrders()
    {
        await _tracker.TrackAsync(SampleOrder("cloid-t4", OrderState.Open), CancellationToken.None);
        await _tracker.TrackAsync(SampleOrder("cloid-t5", OrderState.PartiallyFilled), CancellationToken.None);
        await _tracker.TrackAsync(SampleOrder("cloid-t6", OrderState.Filled), CancellationToken.None);

        var open = new List<OrderResult>();
        await foreach (var o in _tracker.GetOpenOrdersAsync(CancellationToken.None))
            open.Add(o);

        open.Should().HaveCount(2);
        open.Select(o => o.ClientOrderId).Should().BeEquivalentTo(["cloid-t4", "cloid-t5"]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OrderResult SampleOrder(string clientOrderId, OrderState state = OrderState.Open)
        => new(clientOrderId, "v-" + clientOrderId, state, 0m, null,
               "hyperliquid", "BTC-USDT", OrderSide.Buy,
               DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
