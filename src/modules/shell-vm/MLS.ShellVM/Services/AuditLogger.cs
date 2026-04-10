using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MLS.ShellVM.Persistence;

namespace MLS.ShellVM.Services;

/// <summary>
/// Writes structured audit records for all shell activity to the
/// partitioned <c>shell_audit_log</c> PostgreSQL table.
/// </summary>
public sealed class AuditLogger(
    IDbContextFactory<ShellVMDbContext> _dbFactory,
    ILogger<AuditLogger> _logger) : IAuditLogger
{
    /// <inheritdoc/>
    public async Task LogCommandStartAsync(AuditEntry entry, CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            db.AuditLog.Add(new AuditEntryEntity
            {
                Id        = entry.Id,
                BlockId   = entry.BlockId,
                Command   = entry.Command,
                StartedAt = entry.StartedAt,
                ModuleId  = entry.ModuleId,
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit start for command {CommandId}", entry.Id);
        }
    }

    /// <inheritdoc/>
    public async Task LogCommandEndAsync(Guid commandId, int exitCode, TimeSpan duration, CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var entity = await db.AuditLog.FindAsync([commandId], ct).ConfigureAwait(false);
            if (entity is null)
            {
                _logger.LogWarning("Audit entry {CommandId} not found for end-log", commandId);
                return;
            }

            entity.EndedAt    = DateTimeOffset.UtcNow;
            entity.ExitCode   = exitCode;
            entity.DurationMs = (long)duration.TotalMilliseconds;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit end for command {CommandId}", commandId);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AuditEntry> QueryAsync(
        AuditQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var q = db.AuditLog.AsNoTracking().AsQueryable();

        if (query.SessionId.HasValue)
            q = q.Where(a => a.BlockId == query.SessionId.Value);
        if (query.From.HasValue)
            q = q.Where(a => a.StartedAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(a => a.StartedAt <= query.To.Value);

        q = q.OrderByDescending(a => a.StartedAt).Take(query.Limit);

        await foreach (var entity in q.AsAsyncEnumerable().WithCancellation(ct).ConfigureAwait(false))
        {
            yield return new AuditEntry
            {
                Id        = entity.Id,
                BlockId   = entity.BlockId,
                Command   = entity.Command,
                StartedAt = entity.StartedAt,
                EndedAt   = entity.EndedAt,
                ExitCode  = entity.ExitCode,
                DurationMs= entity.DurationMs,
                ModuleId  = entity.ModuleId,
            };
        }
    }
}
