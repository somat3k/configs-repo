using Microsoft.EntityFrameworkCore;

namespace MLS.AIHub.Persistence;

/// <summary>
/// EF Core database context for the AI Hub module.
/// Manages per-user LLM provider preferences.
/// Conversation history persistence will be added in a future session.
/// </summary>
public sealed class AIHubDbContext(DbContextOptions<AIHubDbContext> options) : DbContext(options)
{
    /// <summary>Per-user LLM provider preferences.</summary>
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPreference>(e =>
        {
            e.ToTable("ai_hub_user_preferences");
            e.HasKey(p => p.UserId);
            e.Property(p => p.PrimaryProviderId).HasMaxLength(64).IsRequired();
            e.Property(p => p.PreferredModelId).HasMaxLength(128).IsRequired();
            e.Property(p => p.FallbackChainRaw).HasMaxLength(512).IsRequired();
            e.Property(p => p.ProviderConfigsJson).HasColumnType("text").IsRequired().HasDefaultValue("{}");
        });
    }
}
