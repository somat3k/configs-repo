using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLS.DeFi.Models;
using MLS.DeFi.Persistence;
using Xunit;

namespace MLS.DeFi.Tests;

/// <summary>
/// Tests for <see cref="TransactionRepository"/> using the EF Core in-memory provider.
/// </summary>
public sealed class TransactionRepositoryTests
{
    private static DeFiDbContext CreateDbContext()
    {
        var opts = new DbContextOptionsBuilder<DeFiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DeFiDbContext(opts);
    }

    private static TransactionEntity BuildEntity(string clientOrderId, string state = "Open") => new()
    {
        ClientOrderId      = clientOrderId,
        VenueOrTxId        = "v-1",
        Symbol             = "BTC-USDT",
        Side               = "Buy",
        OrderType          = "Limit",
        Quantity           = 0.5m,
        LimitPrice         = 65_000m,
        State              = state,
        FilledQuantity     = 0m,
        Venue              = "hyperliquid",
        RequestingModuleId = "trader",
        CreatedAt          = DateTimeOffset.UtcNow,
        UpdatedAt          = DateTimeOffset.UtcNow,
    };

    // ── InsertAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Insert_PersistsNewTransaction()
    {
        await using var db   = CreateDbContext();
        var repo             = new TransactionRepository(db);
        var entity           = BuildEntity("cloid-1");

        await repo.InsertAsync(entity);

        var found = await repo.GetByClientOrderIdAsync("cloid-1");
        found.Should().NotBeNull();
        found!.Symbol.Should().Be("BTC-USDT");
    }

    [Fact]
    public async Task Insert_Idempotent_DuplicateIsIgnored()
    {
        await using var db = CreateDbContext();
        var repo           = new TransactionRepository(db);
        var entity         = BuildEntity("cloid-dup");

        await repo.InsertAsync(entity);
        await repo.InsertAsync(entity); // should not throw

        var all = await db.Transactions.CountAsync();
        all.Should().Be(1);
    }

    // ── UpdateStateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateState_ExistingTransaction_UpdatesFields()
    {
        await using var db = CreateDbContext();
        var repo           = new TransactionRepository(db);
        var entity         = BuildEntity("cloid-upd");

        await repo.InsertAsync(entity);

        var updated = await repo.UpdateStateAsync(
            "cloid-upd", DeFiOrderState.Filled.ToString(), 0.5m, 65_000m);

        updated.Should().BeTrue();

        var found = await repo.GetByClientOrderIdAsync("cloid-upd");
        found!.State.Should().Be("Filled");
        found.FilledQuantity.Should().Be(0.5m);
        found.AveragePrice.Should().Be(65_000m);
    }

    [Fact]
    public async Task UpdateState_NonExistentTransaction_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var repo           = new TransactionRepository(db);

        var updated = await repo.UpdateStateAsync("non-existent", "Filled", 0m, null);
        updated.Should().BeFalse();
    }

    // ── GetByClientOrderIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetByClientOrderId_NotFound_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var repo           = new TransactionRepository(db);

        var result = await repo.GetByClientOrderIdAsync("missing-id");
        result.Should().BeNull();
    }

    // ── GetOpenTransactionsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetOpenTransactions_ReturnsOnlyOpenOrPending()
    {
        await using var db = CreateDbContext();
        var repo           = new TransactionRepository(db);

        await repo.InsertAsync(BuildEntity("cloid-open",    DeFiOrderState.Open.ToString()));
        await repo.InsertAsync(BuildEntity("cloid-pending", DeFiOrderState.Pending.ToString()));
        await repo.InsertAsync(BuildEntity("cloid-partial", DeFiOrderState.PartiallyFilled.ToString()));
        await repo.InsertAsync(BuildEntity("cloid-filled",  DeFiOrderState.Filled.ToString()));
        await repo.InsertAsync(BuildEntity("cloid-cancel",  DeFiOrderState.Cancelled.ToString()));

        var open = new List<TransactionEntity>();
        await foreach (var tx in repo.GetOpenTransactionsAsync())
            open.Add(tx);

        open.Should().HaveCount(3);
        open.Select(t => t.ClientOrderId)
            .Should().BeEquivalentTo(["cloid-open", "cloid-pending", "cloid-partial"]);
    }
}
