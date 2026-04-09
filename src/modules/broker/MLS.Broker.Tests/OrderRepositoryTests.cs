using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLS.Broker.Models;
using MLS.Broker.Persistence;
using Xunit;

namespace MLS.Broker.Tests;

/// <summary>
/// Tests for <see cref="OrderRepository"/> using an in-memory EF Core database.
/// </summary>
public sealed class OrderRepositoryTests : IAsyncDisposable
{
    private readonly BrokerDbContext _db;
    private readonly OrderRepository _repo;

    public OrderRepositoryTests()
    {
        var opts = new DbContextOptionsBuilder<BrokerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db   = new BrokerDbContext(opts);
        _repo = new OrderRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync().ConfigureAwait(false);
    }

    // ── InsertAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertAsync_StoresNewOrder()
    {
        var entity = BuildOrder("cloid-1");
        await _repo.InsertAsync(entity);

        var found = await _repo.FindAsync("cloid-1");
        found.Should().NotBeNull();
        found!.Symbol.Should().Be("BTC-USDT");
    }

    [Fact]
    public async Task InsertAsync_IsIdempotent_WhenClientOrderIdAlreadyExists()
    {
        var entity = BuildOrder("cloid-2");
        await _repo.InsertAsync(entity);
        await _repo.InsertAsync(entity); // second call should no-op

        var count = await _db.Orders.CountAsync(o => o.ClientOrderId == "cloid-2");
        count.Should().Be(1);
    }

    // ── UpdateStateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStateAsync_UpdatesExistingOrder()
    {
        await _repo.InsertAsync(BuildOrder("cloid-3"));

        var updated = await _repo.UpdateStateAsync(
            "cloid-3", OrderState.Filled.ToString(), 0.5m, 65_000m);

        updated.Should().BeTrue();

        var found = await _repo.FindAsync("cloid-3");
        found!.State.Should().Be("Filled");
        found.FilledQuantity.Should().Be(0.5m);
        found.AveragePrice.Should().Be(65_000m);
    }

    [Fact]
    public async Task UpdateStateAsync_ReturnsFalse_WhenOrderNotFound()
    {
        var result = await _repo.UpdateStateAsync("nonexistent", "Filled", 1m, null);
        result.Should().BeFalse();
    }

    // ── GetOpenOrdersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOpenOrdersAsync_ReturnsOnlyOpenOrders()
    {
        await _repo.InsertAsync(BuildOrder("cloid-4", OrderState.Open));
        await _repo.InsertAsync(BuildOrder("cloid-5", OrderState.PartiallyFilled));
        await _repo.InsertAsync(BuildOrder("cloid-6", OrderState.Filled));
        await _repo.InsertAsync(BuildOrder("cloid-7", OrderState.Cancelled));

        var open = new List<OrderEntity>();
        await foreach (var o in _repo.GetOpenOrdersAsync())
            open.Add(o);

        open.Should().HaveCount(2);
        open.Select(o => o.ClientOrderId).Should().BeEquivalentTo(["cloid-4", "cloid-5"]);
    }

    // ── UpsertPositionAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPositionAsync_InsertsNewPosition()
    {
        var pos = BuildPosition("BTC-USDT", "hyperliquid");
        await _repo.UpsertPositionAsync(pos);

        var found = await _repo.GetPositionAsync("BTC-USDT", "hyperliquid");
        found.Should().NotBeNull();
        found!.Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task UpsertPositionAsync_UpdatesExistingPosition()
    {
        await _repo.UpsertPositionAsync(BuildPosition("ETH-USDT", "hyperliquid", qty: 2m));
        await _repo.UpsertPositionAsync(BuildPosition("ETH-USDT", "hyperliquid", qty: 3m));

        var found = await _repo.GetPositionAsync("ETH-USDT", "hyperliquid");
        found!.Quantity.Should().Be(3m);

        var count = await _db.Positions.CountAsync(p => p.Symbol == "ETH-USDT");
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetPositionAsync_ReturnsNull_WhenNotFound()
    {
        var found = await _repo.GetPositionAsync("NOTEXIST-USDT", "hyperliquid");
        found.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static OrderEntity BuildOrder(string clientOrderId, OrderState state = OrderState.Open) => new()
    {
        ClientOrderId      = clientOrderId,
        Symbol             = "BTC-USDT",
        Side               = OrderSide.Buy.ToString(),
        OrderType          = Models.OrderType.Limit.ToString(),
        Quantity           = 0.5m,
        LimitPrice         = 65_000m,
        State              = state.ToString(),
        FilledQuantity     = 0m,
        Venue              = "hyperliquid",
        RequestingModuleId = "trader",
    };

    private static PositionEntity BuildPosition(string symbol, string venue, decimal qty = 1m) => new()
    {
        Symbol             = symbol,
        Side               = OrderSide.Buy.ToString(),
        Quantity           = qty,
        AverageEntryPrice  = 60_000m,
        UnrealisedPnl      = 1_000m,
        Venue              = venue,
    };
}
