using Microsoft.EntityFrameworkCore;

namespace MLS.Trader.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the Trader module.
/// Manages the <see cref="TradeEntity"/> (<c>trader_orders</c>) and
/// <see cref="PositionEntity"/> (<c>trader_positions</c>) tables.
/// </summary>
public sealed class TraderDbContext(DbContextOptions<TraderDbContext> options) : DbContext(options)
{
    /// <summary>Trader order state table.</summary>
    public DbSet<TradeEntity> Orders { get; set; } = default!;

    /// <summary>Open positions table.</summary>
    public DbSet<PositionEntity> Positions { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── trader_orders ─────────────────────────────────────────────────────
        var order = modelBuilder.Entity<TradeEntity>();
        order.ToTable("trader_orders");

        order.HasIndex(o => o.ClientOrderId)
             .HasDatabaseName("ix_trader_orders_client_order_id")
             .IsUnique(true);

        order.HasIndex(o => o.State)
             .HasDatabaseName("ix_trader_orders_state");

        order.HasIndex(o => o.Symbol)
             .HasDatabaseName("ix_trader_orders_symbol");

        order.Property(o => o.Quantity).HasPrecision(30, 10);
        order.Property(o => o.EntryPrice).HasPrecision(30, 10);
        order.Property(o => o.StopLossPrice).HasPrecision(30, 10);
        order.Property(o => o.TakeProfitPrice).HasPrecision(30, 10);

        // ── trader_positions ──────────────────────────────────────────────────
        var pos = modelBuilder.Entity<PositionEntity>();
        pos.ToTable("trader_positions");

        // One row per (symbol, venue) — upsert on update
        pos.HasIndex(p => new { p.Symbol, p.Venue })
           .HasDatabaseName("ix_trader_positions_symbol_venue")
           .IsUnique(true);

        pos.Property(p => p.Quantity).HasPrecision(30, 10);
        pos.Property(p => p.AverageEntryPrice).HasPrecision(30, 10);
        pos.Property(p => p.UnrealisedPnl).HasPrecision(30, 10);

        base.OnModelCreating(modelBuilder);
    }
}
