using Microsoft.EntityFrameworkCore;
using MLS.DataLayer.FeatureStore;

namespace MLS.DataLayer.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the Data Layer module.
/// Manages the <see cref="CandleEntity"/> and <see cref="FeatureStoreEntity"/> tables
/// (<c>candles</c> and <c>feature_store_vectors</c>) in the MLS PostgreSQL database.
/// </summary>
public sealed class DataLayerDbContext(DbContextOptions<DataLayerDbContext> options) : DbContext(options)
{
    /// <summary>OHLCV candles table.</summary>
    public DbSet<CandleEntity> Candles { get; set; } = default!;

    /// <summary>Computed feature vectors table.</summary>
    public DbSet<FeatureStoreEntity> FeatureStore { get; set; } = default!;

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

        // ── feature_store_vectors ──────────────────────────────────────────────

        var feature = modelBuilder.Entity<FeatureStoreEntity>();

        feature.ToTable("feature_store_vectors");

        // Composite unique index: one feature vector per (exchange, symbol, timeframe, model_type, feature_timestamp)
        feature.HasIndex(f => new { f.Exchange, f.Symbol, f.Timeframe, f.ModelType, f.FeatureTimestamp })
               .HasDatabaseName("ix_fsvectors_feed_model_ts")
               .IsUnique(true);

        feature.Property(f => f.Exchange).HasMaxLength(64);
        feature.Property(f => f.Symbol).HasMaxLength(32);
        feature.Property(f => f.Timeframe).HasMaxLength(8);
        feature.Property(f => f.ModelType).HasMaxLength(16);
        feature.Property(f => f.FeaturesJson).HasColumnType("jsonb");

        base.OnModelCreating(modelBuilder);
    }
}
