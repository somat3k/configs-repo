using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MLS.ShellVM.Persistence;
using StackExchange.Redis;

namespace MLS.ShellVM.Services;

/// <summary>
/// Manages the lifecycle of shell execution sessions.
/// In-memory state is authoritative for running sessions;
/// Redis provides durability across WebSocket reconnections.
/// </summary>
public sealed class SessionManager(
    IDbContextFactory<ShellVMDbContext> _dbFactory,
    IConnectionMultiplexer? _redis,
    IPtyProvider _pty,
    IOptions<ShellVMConfig> _config,
    ILogger<SessionManager> _logger) : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, ShellSession> _sessions = new();

    /// <inheritdoc/>
    public int ActiveSessionCount =>
        _sessions.Values.Count(s =>
            s.Block.State is ExecutionBlockState.Created
                          or ExecutionBlockState.Running
                          or ExecutionBlockState.Starting
                          or ExecutionBlockState.Paused);

    /// <inheritdoc/>
    public async Task<ShellSession> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct)
    {
        var cfg = _config.Value;

        if (ActiveSessionCount >= cfg.MaxConcurrentSessions)
            throw new InvalidOperationException(
                $"Session limit reached ({cfg.MaxConcurrentSessions}). Refuse to create new session.");

        var effectiveShell = string.IsNullOrWhiteSpace(request.Shell) ? cfg.DefaultShell : request.Shell;

        if (!cfg.AllowedShells.Contains(effectiveShell))
            throw new ArgumentException(
                $"Shell '{effectiveShell}' is not in the allow-list.", nameof(request));

        var block = new ExecutionBlock
        {
            Label              = request.Label,
            Shell              = effectiveShell,
            WorkingDirectory   = request.WorkingDirectory,
            Environment        = request.Environment?.ToDictionary() ?? [],
            RequestingModuleId = request.RequestingModuleId,
            State              = ExecutionBlockState.Created,
        };

        // Persist to PostgreSQL
        await PersistBlockAsync(block, ct).ConfigureAwait(false);

        var outputChannel = Channel.CreateBounded<OutputChunk>(
            new BoundedChannelOptions(ShellVMLimits.OutputChannelCapacity)
            {
                FullMode     = BoundedChannelFullMode.DropOldest,
                SingleWriter = false,
                SingleReader = false,
            });

        var session = new ShellSession(block, PtyHandle: null, outputChannel);
        _sessions[block.Id] = session;

        _logger.LogInformation("Session {Id} created (shell={Shell})",
            block.Id, SanitiseForLog(block.Shell));
        return session;
    }

    /// <inheritdoc/>
    public async Task TerminateSessionAsync(Guid sessionId, bool graceful, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;

        var previous = session.Block.State;
        session.Block.State       = ExecutionBlockState.Terminated;
        session.Block.CompletedAt = DateTimeOffset.UtcNow;

        if (session.PtyHandle is not null)
        {
            try
            {
                await _pty.KillAsync(session.PtyHandle, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PTY kill failed for session {Id}", sessionId);
            }
        }

        session.OutputChannel.Writer.TryComplete();

        _sessions.TryRemove(sessionId, out _);
        await UpdateBlockStateAsync(sessionId, ExecutionBlockState.Terminated, null, ct).ConfigureAwait(false);
        await RemoveSessionFromRedisAsync(sessionId, ct).ConfigureAwait(false);

        _logger.LogInformation("Session {Id} terminated (previous={Previous}, graceful={Graceful})",
            sessionId, previous, graceful);
    }

    /// <inheritdoc/>
    public Task<ShellSession?> GetSessionAsync(Guid sessionId, CancellationToken ct)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ShellSession> GetSessionsAsync(
        ExecutionBlockState? filter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var session in _sessions.Values)
        {
            if (ct.IsCancellationRequested) yield break;
            if (filter is null || session.Block.State == filter.Value)
                yield return session;
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task PersistSessionStateAsync(Guid sessionId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;

        if (_redis is null) return;

        try
        {
            var db  = _redis.GetDatabase();
            var key = $"{ShellVMLimits.RedisSessionPrefix}{sessionId}";
            var json = JsonSerializer.Serialize(new
            {
                id                 = session.Block.Id,
                label              = session.Block.Label,
                state              = session.Block.State.ToString(),
                shell              = session.Block.Shell,
                working_directory  = session.Block.WorkingDirectory,
                requesting_module  = session.Block.RequestingModuleId,
                created_at         = session.Block.CreatedAt,
                started_at         = session.Block.StartedAt,
            });
            await db.StringSetAsync(key, json, ShellVMLimits.SessionRedisTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist session {Id} state to Redis", sessionId);
        }
    }

    // ── ISessionManager — state transition and PTY attachment ─────────────────

    /// <inheritdoc/>
    public async Task TransitionStateAsync(
        Guid sessionId,
        ExecutionBlockState newState,
        int? exitCode,
        CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;

        session.Block.State = newState;
        if (newState is ExecutionBlockState.Running && session.Block.StartedAt is null)
            session.Block.StartedAt = DateTimeOffset.UtcNow;
        if (newState is ExecutionBlockState.Completed or ExecutionBlockState.Error or ExecutionBlockState.Terminated)
        {
            session.Block.CompletedAt = DateTimeOffset.UtcNow;
            session.Block.ExitCode    = exitCode;
        }

        await UpdateBlockStateAsync(sessionId, newState, exitCode, ct).ConfigureAwait(false);
        await PersistSessionStateAsync(sessionId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void AttachPtyHandle(Guid sessionId, PtyHandle handle)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;
        _sessions[sessionId] = session with { PtyHandle = handle };
    }

    /// <inheritdoc/>
    public void UpdateLastActivity(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            session.Block.LastActivityAt = DateTimeOffset.UtcNow;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task PersistBlockAsync(ExecutionBlock block, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = new ExecutionBlockEntity
        {
            Id                 = block.Id,
            Label              = block.Label,
            State              = block.State.ToString(),
            Shell              = block.Shell,
            WorkingDirectory   = block.WorkingDirectory,
            Environment        = block.Environment.Count > 0
                                     ? JsonSerializer.Serialize(block.Environment)
                                     : null,
            RequestingModuleId = block.RequestingModuleId,
            CreatedAt          = block.CreatedAt,
        };
        db.ExecutionBlocks.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task UpdateBlockStateAsync(
        Guid sessionId, ExecutionBlockState state, int? exitCode, CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var entity = await db.ExecutionBlocks.FindAsync([sessionId], ct).ConfigureAwait(false);
            if (entity is null) return;

            entity.State = state.ToString();
            if (exitCode.HasValue)  entity.ExitCode    = exitCode;
            if (state is ExecutionBlockState.Running)
                entity.StartedAt = DateTimeOffset.UtcNow;
            if (state is ExecutionBlockState.Completed or ExecutionBlockState.Error or ExecutionBlockState.Terminated)
                entity.CompletedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update block state for session {Id}", sessionId);
        }
    }

    private async Task RemoveSessionFromRedisAsync(Guid sessionId, CancellationToken ct)
    {
        if (_redis is null) return;
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"{ShellVMLimits.RedisSessionPrefix}{sessionId}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove session {Id} from Redis", sessionId);
        }
    }

    /// <summary>
    /// Strips CR/LF characters from a user-supplied string before it is written to a log sink,
    /// preventing log-forging attacks.
    /// </summary>
    private static string SanitiseForLog(string? value) =>
        (value ?? string.Empty)
            .Replace('\r', '_')
            .Replace('\n', '_');
}
