using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MLS.ShellVM.Models;

namespace MLS.ShellVM.Persistence;

/// <summary>
/// EF Core <see cref="DbContext"/> for the Shell VM module.
/// Manages the <see cref="ExecutionBlockEntity"/> (<c>execution_blocks</c>) and
/// <see cref="AuditEntryEntity"/> (<c>shell_audit_log</c>) tables.
/// </summary>
public sealed class ShellVMDbContext(DbContextOptions<ShellVMDbContext> options) : DbContext(options)
{
    /// <summary>Execution block registry table.</summary>
    public DbSet<ExecutionBlockEntity> ExecutionBlocks { get; set; } = default!;

    /// <summary>Shell audit log table.</summary>
    public DbSet<AuditEntryEntity> AuditLog { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── execution_blocks ──────────────────────────────────────────────────
        var block = modelBuilder.Entity<ExecutionBlockEntity>();
        block.ToTable("execution_blocks");
        block.HasIndex(b => b.State).HasDatabaseName("ix_execution_blocks_state");
        block.HasIndex(b => b.CreatedAt).HasDatabaseName("ix_execution_blocks_created_at");

        // ── shell_audit_log ───────────────────────────────────────────────────
        // The partitioned table is created via raw SQL in Program.cs.
        // EF Core uses the table as if it were a plain table for reads/writes.
        var audit = modelBuilder.Entity<AuditEntryEntity>();
        audit.ToTable("shell_audit_log");
        audit.HasIndex(a => a.BlockId).HasDatabaseName("ix_audit_block_id");
        audit.HasIndex(a => a.StartedAt).HasDatabaseName("ix_audit_started_at");

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Persisted representation of an <see cref="ExecutionBlock"/> in the
/// <c>execution_blocks</c> PostgreSQL table.
/// </summary>
[Table("execution_blocks")]
public sealed class ExecutionBlockEntity
{
    /// <summary>Primary key — matches the <see cref="ExecutionBlock.Id"/>.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Human-readable label.</summary>
    [Required, MaxLength(256)]
    [Column("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>State enum value stored as text.</summary>
    [Required, MaxLength(32)]
    [Column("state")]
    public string State { get; set; } = ExecutionBlockState.Created.ToString();

    /// <summary>Shell executable (e.g. <c>/bin/sh</c>).</summary>
    [Required, MaxLength(128)]
    [Column("shell")]
    public string Shell { get; set; } = "/bin/sh";

    /// <summary>Working directory for the process.</summary>
    [Required, MaxLength(512)]
    [Column("working_directory")]
    public string WorkingDirectory { get; set; } = "/app";

    /// <summary>JSON-serialised environment variables.</summary>
    [Column("environment", TypeName = "jsonb")]
    public string? Environment { get; set; }

    /// <summary>Module ID of the requesting entity.</summary>
    [MaxLength(64)]
    [Column("requesting_module_id")]
    public string? RequestingModuleId { get; set; }

    /// <summary>UTC creation timestamp.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC start timestamp.</summary>
    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>UTC completion timestamp.</summary>
    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Process exit code.</summary>
    [Column("exit_code")]
    public int? ExitCode { get; set; }
}

/// <summary>
/// Persisted audit record in the partitioned <c>shell_audit_log</c> table.
/// </summary>
[Table("shell_audit_log")]
public sealed class AuditEntryEntity
{
    /// <summary>Surrogate audit record identifier.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>FK to <c>execution_blocks.id</c>.</summary>
    [Column("block_id")]
    public Guid BlockId { get; set; }

    /// <summary>The command that was executed.</summary>
    [Required]
    [Column("command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>UTC start timestamp (used as the partition key).</summary>
    [Column("started_at")]
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>UTC end timestamp.</summary>
    [Column("ended_at")]
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Process exit code.</summary>
    [Column("exit_code")]
    public int? ExitCode { get; set; }

    /// <summary>Total execution duration in milliseconds.</summary>
    [Column("duration_ms")]
    public long? DurationMs { get; set; }

    /// <summary>Module ID of the triggering entity.</summary>
    [MaxLength(64)]
    [Column("module_id")]
    public string? ModuleId { get; set; }
}
