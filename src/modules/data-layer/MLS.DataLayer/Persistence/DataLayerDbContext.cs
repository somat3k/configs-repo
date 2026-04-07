using Microsoft.EntityFrameworkCore;

namespace MLS.DataLayer.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the Data Layer module.
/// Manages the <see cref="CandleEntity"/> table in the MLS PostgreSQL database.
/// </summary>
public sealed class DataLayerDbContext(DbContextOptions<DataLayerDbContext> options) : DbContext(options)
{
    /// <summary>OHLCV candles table.</summary>
    public DbSet<CandleEntity> Candles { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var candle = modelBuilder.Entity<CandleEntity>();

        candle.ToTable("candles");

        // Composite unique index: one candle per (exchange, symbol, timeframe, open_time)
        candle.HasIndex(c => new { c.Exchange, c.Symbol, c.Timeframe, c.OpenTime })
              .HasDatabaseName("ix_candles_feed_open_time")
              .IsUnique(true);

        // Fast range queries for gap detection
        candle.HasIndex(c => new { c.Exchange, c.Symbol, c.Timeframe, c.OpenTime })
              .HasDatabaseName("ix_candles_open_time_brin")
              .HasMethod("brin");

        candle.Property(c => c.Exchange).HasMaxLength(64);
        candle.Property(c => c.Symbol).HasMaxLength(32);
        candle.Property(c => c.Timeframe).HasMaxLength(8);

        base.OnModelCreating(modelBuilder);
    }
}
