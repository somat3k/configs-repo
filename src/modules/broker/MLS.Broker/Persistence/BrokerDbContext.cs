using Microsoft.EntityFrameworkCore;

namespace MLS.Broker.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the Broker module.
/// Manages the <see cref="OrderEntity"/> (<c>orders</c>) and
/// <see cref="PositionEntity"/> (<c>positions</c>) tables.
/// </summary>
public sealed class BrokerDbContext(DbContextOptions<BrokerDbContext> options) : DbContext(options)
{
    /// <summary>Order state table.</summary>
    public DbSet<OrderEntity> Orders { get; set; } = default!;

    /// <summary>Open positions table.</summary>
    public DbSet<PositionEntity> Positions { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── orders ────────────────────────────────────────────────────────────
        var order = modelBuilder.Entity<OrderEntity>();
        order.ToTable("orders");

        // Unique constraint on client_order_id for idempotency
        order.HasIndex(o => o.ClientOrderId)
             .HasDatabaseName("ix_orders_client_order_id")
             .IsUnique(true);

        // Fast state queries (fetch open/partial orders)
        order.HasIndex(o => o.State)
             .HasDatabaseName("ix_orders_state");

        order.Property(o => o.Quantity).HasPrecision(30, 10);
        order.Property(o => o.LimitPrice).HasPrecision(30, 10);
        order.Property(o => o.StopPrice).HasPrecision(30, 10);
        order.Property(o => o.FilledQuantity).HasPrecision(30, 10);
        order.Property(o => o.AveragePrice).HasPrecision(30, 10);

        // ── positions ─────────────────────────────────────────────────────────
        var pos = modelBuilder.Entity<PositionEntity>();
        pos.ToTable("positions");

        // One row per (symbol, venue) — upsert on update
        pos.HasIndex(p => new { p.Symbol, p.Venue })
           .HasDatabaseName("ix_positions_symbol_venue")
           .IsUnique(true);

        pos.Property(p => p.Quantity).HasPrecision(30, 10);
        pos.Property(p => p.AverageEntryPrice).HasPrecision(30, 10);
        pos.Property(p => p.UnrealisedPnl).HasPrecision(30, 10);

        base.OnModelCreating(modelBuilder);
    }
}
