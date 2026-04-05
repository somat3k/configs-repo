using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MLS.Designer.Persistence;

/// <summary>
/// Entity Framework Core entity for a persisted MLS strategy graph schema.
/// Maps to the <c>strategy_schemas</c> table in the MLS PostgreSQL database.
/// </summary>
[Table("strategy_schemas")]
public sealed class StrategySchema
{
    /// <summary>Primary key — strategy graph identifier.</summary>
    [Key]
    [Column("graph_id")]
    public Guid GraphId { get; set; }

    /// <summary>Human-readable strategy name, e.g. <c>"Momentum Long BTC"</c>.</summary>
    [Required]
    [MaxLength(256)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Strategy type category, e.g. <c>trading</c>, <c>arbitrage</c>, <c>defi</c>, <c>ml-training</c>.
    /// </summary>
    [MaxLength(64)]
    [Column("strategy_type")]
    public string StrategyType { get; set; } = "trading";

    /// <summary>
    /// Deployment status: <c>draft</c>, <c>deployed</c>, <c>stopped</c>, <c>backtesting</c>.
    /// </summary>
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = "draft";

    /// <summary>
    /// Monotonically incrementing version counter.  Incremented on every structural change
    /// (block add/remove, connection change, parameter mutation).
    /// </summary>
    [Column("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Full strategy graph payload serialised as JSON (blocks + connections).
    /// Stored as JSONB in PostgreSQL for efficient querying.
    /// </summary>
    [Column("graph_json", TypeName = "jsonb")]
    public string GraphJson { get; set; } = "{}";

    /// <summary>Optional description / notes shown in the canvas sidebar.</summary>
    [MaxLength(2048)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Source template name, if the strategy was created from a template.</summary>
    [MaxLength(128)]
    [Column("template_name")]
    public string? TemplateName { get; set; }

    /// <summary>UTC timestamp when the strategy was first created.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the last structural change.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-delete flag.  Soft-deleted strategies are hidden from list queries.</summary>
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
