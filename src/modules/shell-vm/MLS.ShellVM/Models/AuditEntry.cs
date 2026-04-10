namespace MLS.ShellVM.Models;

/// <summary>
/// Structured audit record for a single command execution in a shell session.
/// Persisted to the partitioned <c>shell_audit_log</c> PostgreSQL table.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>Unique audit record identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Session (execution block) that ran the command.</summary>
    public Guid BlockId { get; init; }

    /// <summary>The full command string that was executed.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>UTC timestamp when command execution began.</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when command execution ended; <see langword="null"/> if still running.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Process exit code; <see langword="null"/> if the command is still in progress.</summary>
    public int? ExitCode { get; set; }

    /// <summary>Total execution time in milliseconds; computed on completion.</summary>
    public long? DurationMs { get; set; }

    /// <summary>Registered module ID of the entity that triggered the command.</summary>
    public string? ModuleId { get; init; }
}
