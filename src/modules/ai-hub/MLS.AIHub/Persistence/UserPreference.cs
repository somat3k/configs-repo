using System.ComponentModel.DataAnnotations;

namespace MLS.AIHub.Persistence;

/// <summary>
/// EF Core entity storing per-user LLM provider preferences.
/// Maps to the <c>ai_hub_user_preferences</c> PostgreSQL table.
/// </summary>
public sealed class UserPreference
{
    /// <summary>User identifier (FK to platform user table).</summary>
    [Key]
    public Guid UserId { get; set; }

    /// <summary>Primary provider ID selected by the user (e.g. <c>"openai"</c>).</summary>
    [MaxLength(64)]
    public string PrimaryProviderId { get; set; } = "openai";

    /// <summary>Preferred model within the primary provider (e.g. <c>"gpt-4o"</c>).</summary>
    [MaxLength(128)]
    public string PreferredModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// Ordered fallback chain overriding the global default.
    /// Stored as a comma-separated string in PostgreSQL.
    /// </summary>
    [MaxLength(512)]
    public string FallbackChainRaw { get; set; } = "openai,anthropic,groq,local";

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Fallback chain parsed from <see cref="FallbackChainRaw"/>.</summary>
    public IReadOnlyList<string> FallbackChain =>
        FallbackChainRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
