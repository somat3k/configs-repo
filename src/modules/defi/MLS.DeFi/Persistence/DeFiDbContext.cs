using Microsoft.EntityFrameworkCore;

namespace MLS.DeFi.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the DeFi module.
/// Manages <see cref="TransactionEntity"/> (<c>defi_transactions</c>) and
/// <see cref="DeFiPositionEntity"/> (<c>defi_positions</c>) tables.
/// </summary>
public sealed class DeFiDbContext(DbContextOptions<DeFiDbContext> options) : DbContext(options)
{
    /// <summary>DeFi transaction records table.</summary>
    public DbSet<TransactionEntity> Transactions { get; set; } = default!;

    /// <summary>Open DeFi positions table.</summary>
    public DbSet<DeFiPositionEntity> Positions { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── defi_transactions ─────────────────────────────────────────────────
        var tx = modelBuilder.Entity<TransactionEntity>();
        tx.ToTable("defi_transactions");

        tx.HasIndex(t => t.ClientOrderId)
          .HasDatabaseName("ix_defi_transactions_client_order_id")
          .IsUnique(true);

        tx.HasIndex(t => t.State)
          .HasDatabaseName("ix_defi_transactions_state");

        tx.Property(t => t.Quantity).HasPrecision(30, 10);
        tx.Property(t => t.LimitPrice).HasPrecision(30, 10);
        tx.Property(t => t.FilledQuantity).HasPrecision(30, 10);
        tx.Property(t => t.AveragePrice).HasPrecision(30, 10);

        // ── defi_positions ────────────────────────────────────────────────────
        var pos = modelBuilder.Entity<DeFiPositionEntity>();
        pos.ToTable("defi_positions");

        pos.HasIndex(p => new { p.Symbol, p.Venue })
           .HasDatabaseName("ix_defi_positions_symbol_venue")
           .IsUnique(true);

        pos.Property(p => p.Quantity).HasPrecision(30, 10);
        pos.Property(p => p.AverageEntryPrice).HasPrecision(30, 10);
        pos.Property(p => p.UnrealisedPnl).HasPrecision(30, 10);

        base.OnModelCreating(modelBuilder);
    }
}
