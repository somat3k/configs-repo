using System.Collections.Concurrent;

namespace MLS.ShellVM.Services;

/// <summary>
/// Executes commands and scripts in sandboxed shell sessions.
/// Dispatches to the PTY provider and manages per-command cancellation tokens.
/// </summary>
public sealed class ExecutionEngine(
    ISessionManager _sessions,
    IPtyProvider _pty,
    IAuditLogger _audit,
    IOutputBroadcaster _broadcaster,
    IOptions<ShellVMConfig> _config,
    ILogger<ExecutionEngine> _logger) : IExecutionEngine
{
    // commandId → CTS (owned by RunCommandAsync; CancelAsync only signals it)
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _commands = new();

    // commandId → spawned PtyHandle so CancelAsync can kill the process
    private readonly ConcurrentDictionary<Guid, PtyHandle> _commandHandles = new();

    /// <inheritdoc/>
    public async Task<CommandExecution> ExecuteAsync(
        Guid sessionId, ExecRequest request, CancellationToken ct)
    {
        var session = await _sessions.GetSessionAsync(sessionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var cfg     = _config.Value;
        var timeout = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : cfg.CommandTimeoutSeconds;

        var commandId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        var execution = new CommandExecution(
            Id:        commandId,
            SessionId: sessionId,
            Command:   request.Command,
            State:     CommandState.Pending,
            StartedAt: startedAt);

        if (cfg.AuditEnabled)
        {
            var auditEntry = new AuditEntry
            {
                Id        = commandId,
                BlockId   = sessionId,
                Command   = request.Command,
                StartedAt = startedAt,
                ModuleId  = session.Block.RequestingModuleId,
            };
            await _audit.LogCommandStartAsync(auditEntry, ct).ConfigureAwait(false);
        }

        // Fire-and-forget: output is streamed via IOutputBroadcaster
        _ = RunCommandAsync(sessionId, commandId, startedAt, request, timeout, ct);

        return execution with { State = CommandState.Running };
    }

    /// <inheritdoc/>
    public Task<CommandExecution> RunScriptAsync(
        Guid sessionId, ScriptRunRequest request, CancellationToken ct)
    {
        var interpreter = request.Interpreter ?? "/bin/sh";

        // Spawn the interpreter directly with the script path + args as separate ArgumentList
        // entries so spaces in paths/arguments are handled correctly without shell injection risk.
        var internalRequest = new ScriptExecRequest(
            Interpreter: interpreter,
            ScriptPath:  request.ScriptPath,
            Arguments:   request.Arguments,
            WorkingDir:  null,
            Env:         request.Env,
            TimeoutSeconds: request.TimeoutSeconds);

        return ExecuteScriptDirectAsync(sessionId, internalRequest, ct);
    }

    /// <inheritdoc/>
    public async Task CancelAsync(Guid commandId, CancellationToken ct)
    {
        // Kill the underlying process first (best-effort), then cancel the CTS.
        if (_commandHandles.TryGetValue(commandId, out var handle))
        {
            try
            {
                await _pty.KillAsync(handle, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CancelAsync: failed to kill PTY for command {CommandId}", commandId);
            }
        }

        if (_commands.TryGetValue(commandId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Command {CommandId} cancellation requested", commandId);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RunCommandAsync(
        Guid sessionId,
        Guid commandId,
        DateTimeOffset startedAt,
        ExecRequest request,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        _commands[commandId] = linkedCts;

        var session = await _sessions.GetSessionAsync(sessionId, CancellationToken.None)
                                     .ConfigureAwait(false);
        if (session is null)
        {
            _logger.LogWarning("RunCommandAsync: session {SessionId} no longer exists", sessionId);
            _commands.TryRemove(commandId, out _);
            return;
        }

        // Capture the handle so cancellation paths can kill the process.
        PtyHandle? handle = null;
        try
        {
            var env  = request.Env?.ToDictionary() ?? [];
            var opts = new PtySpawnOptions(
                Executable:       session.Block.Shell,
                Arguments:        ["-c", request.Command],
                WorkingDirectory: request.WorkingDir ?? session.Block.WorkingDirectory,
                Environment:      env);

            handle = await _pty.SpawnAsync(opts, linkedCts.Token).ConfigureAwait(false);
            _commandHandles[commandId] = handle;
            _sessions.AttachPtyHandle(sessionId, handle);

            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Running, null, linkedCts.Token).ConfigureAwait(false);

            if (request.CaptureOutput)
            {
                await foreach (var chunk in _pty.ReadOutputAsync(handle, sessionId, linkedCts.Token)
                                               .ConfigureAwait(false))
                {
                    _sessions.UpdateLastActivity(sessionId);
                    await _broadcaster.BroadcastChunkAsync(chunk, linkedCts.Token)
                                      .ConfigureAwait(false);
                }
            }

            var exitCode = await _pty.WaitForExitAsync(handle, linkedCts.Token).ConfigureAwait(false);
            var newState = exitCode == 0 ? ExecutionBlockState.Completed : ExecutionBlockState.Error;

            await _sessions.TransitionStateAsync(
                sessionId, newState, exitCode, CancellationToken.None).ConfigureAwait(false);

            if (_config.Value.AuditEnabled)
            {
                var duration = DateTimeOffset.UtcNow - startedAt;
                await _audit.LogCommandEndAsync(commandId, exitCode, duration, CancellationToken.None)
                             .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Command {CommandId} in session {SessionId} cancelled/timed-out",
                commandId, sessionId);
            if (handle is not null)
                await TryKillAsync(handle, commandId).ConfigureAwait(false);
            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandId} in session {SessionId} failed",
                commandId, sessionId);
            if (handle is not null)
                await TryKillAsync(handle, commandId).ConfigureAwait(false);
            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _commands.TryRemove(commandId, out _);
            _commandHandles.TryRemove(commandId, out _);
        }
    }

    /// <summary>Executes a script by spawning the interpreter directly (no shell quoting needed).</summary>
    private async Task<CommandExecution> ExecuteScriptDirectAsync(
        Guid sessionId, ScriptExecRequest request, CancellationToken ct)
    {
        var session = await _sessions.GetSessionAsync(sessionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var cfg     = _config.Value;
        var timeout = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : cfg.CommandTimeoutSeconds;

        var commandId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var commandSummary = $"{request.Interpreter} {request.ScriptPath} " +
                             string.Join(' ', request.Arguments);

        var execution = new CommandExecution(
            Id:        commandId,
            SessionId: sessionId,
            Command:   commandSummary,
            State:     CommandState.Pending,
            StartedAt: startedAt);

        if (cfg.AuditEnabled)
        {
            var auditEntry = new AuditEntry
            {
                Id        = commandId,
                BlockId   = sessionId,
                Command   = commandSummary,
                StartedAt = startedAt,
                ModuleId  = session.Block.RequestingModuleId,
            };
            await _audit.LogCommandStartAsync(auditEntry, ct).ConfigureAwait(false);
        }

        _ = RunScriptDirectAsync(sessionId, commandId, startedAt, session, request, timeout, ct);
        return execution with { State = CommandState.Running };
    }

    private async Task RunScriptDirectAsync(
        Guid sessionId,
        Guid commandId,
        DateTimeOffset startedAt,
        ShellSession session,
        ScriptExecRequest request,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        _commands[commandId] = linkedCts;

        PtyHandle? handle = null;
        try
        {
            // Build ArgumentList directly — interpreter, then script path, then args.
            // This avoids any shell quoting or injection issues.
            var args = new[] { request.ScriptPath }
                .Concat(request.Arguments)
                .ToArray();

            var env  = request.Env?.ToDictionary() ?? [];
            var opts = new PtySpawnOptions(
                Executable:       request.Interpreter,
                Arguments:        args,
                WorkingDirectory: request.WorkingDir ?? session.Block.WorkingDirectory,
                Environment:      env);

            handle = await _pty.SpawnAsync(opts, linkedCts.Token).ConfigureAwait(false);
            _commandHandles[commandId] = handle;
            _sessions.AttachPtyHandle(sessionId, handle);

            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Running, null, linkedCts.Token).ConfigureAwait(false);

            await foreach (var chunk in _pty.ReadOutputAsync(handle, sessionId, linkedCts.Token)
                                           .ConfigureAwait(false))
            {
                _sessions.UpdateLastActivity(sessionId);
                await _broadcaster.BroadcastChunkAsync(chunk, linkedCts.Token)
                                  .ConfigureAwait(false);
            }

            var exitCode = await _pty.WaitForExitAsync(handle, linkedCts.Token).ConfigureAwait(false);
            var newState = exitCode == 0 ? ExecutionBlockState.Completed : ExecutionBlockState.Error;

            await _sessions.TransitionStateAsync(
                sessionId, newState, exitCode, CancellationToken.None).ConfigureAwait(false);

            if (_config.Value.AuditEnabled)
            {
                var duration = DateTimeOffset.UtcNow - startedAt;
                await _audit.LogCommandEndAsync(commandId, exitCode, duration, CancellationToken.None)
                             .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Script {CommandId} in session {SessionId} cancelled/timed-out",
                commandId, sessionId);
            if (handle is not null)
                await TryKillAsync(handle, commandId).ConfigureAwait(false);
            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script {CommandId} in session {SessionId} failed",
                commandId, sessionId);
            if (handle is not null)
                await TryKillAsync(handle, commandId).ConfigureAwait(false);
            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _commands.TryRemove(commandId, out _);
            _commandHandles.TryRemove(commandId, out _);
        }
    }

    private async Task TryKillAsync(PtyHandle handle, Guid commandId)
    {
        try { await _pty.KillAsync(handle, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill PTY for command {CommandId}", commandId);
        }
    }

    // ── Private types ─────────────────────────────────────────────────────────

    /// <summary>Internal transfer object used to avoid string-concat injection in script execution.</summary>
    private sealed record ScriptExecRequest(
        string Interpreter,
        string ScriptPath,
        string[] Arguments,
        string? WorkingDir,
        IReadOnlyDictionary<string, string>? Env,
        int TimeoutSeconds);
}

