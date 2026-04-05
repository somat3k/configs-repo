using Microsoft.EntityFrameworkCore;

namespace MLS.Designer.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the Designer module.
/// Manages the <see cref="StrategySchema"/> entities in the MLS PostgreSQL database.
/// </summary>
public sealed class DesignerDbContext(DbContextOptions<DesignerDbContext> options) : DbContext(options)
{
    /// <summary>Strategy schemas table.</summary>
    public DbSet<StrategySchema> Strategies { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var strategy = modelBuilder.Entity<StrategySchema>();

        // Ensure GraphId is treated as uuid in PostgreSQL
        strategy.Property(s => s.GraphId)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        // JSONB column for graph payload
        strategy.Property(s => s.GraphJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        // Unique index on (Name, IsDeleted=false) to prevent duplicate active strategy names
        strategy.HasIndex(s => new { s.Name, s.IsDeleted })
            .HasDatabaseName("ix_strategy_schemas_name_active")
            .IsUnique(true);

        base.OnModelCreating(modelBuilder);
    }
}
