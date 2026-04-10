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
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _commands = new();

    /// <inheritdoc/>
    public async Task<CommandExecution> ExecuteAsync(
        Guid sessionId, ExecRequest request, CancellationToken ct)
    {
        var session = await _sessions.GetSessionAsync(sessionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var cfg     = _config.Value;
        var timeout = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : cfg.CommandTimeoutSeconds;

        var commandId = Guid.NewGuid();
        var execution = new CommandExecution(
            Id:        commandId,
            SessionId: sessionId,
            Command:   request.Command,
            State:     CommandState.Pending,
            StartedAt: DateTimeOffset.UtcNow);

        var auditEntry = new AuditEntry
        {
            Id        = commandId,
            BlockId   = sessionId,
            Command   = request.Command,
            StartedAt = execution.StartedAt,
            ModuleId  = session.Block.RequestingModuleId,
        };

        if (cfg.AuditEnabled)
            await _audit.LogCommandStartAsync(auditEntry, ct).ConfigureAwait(false);

        // Launch execution on background task
        _ = RunCommandAsync(session, commandId, request, timeout, ct);

        return execution with { State = CommandState.Running };
    }

    /// <inheritdoc/>
    public Task<CommandExecution> RunScriptAsync(
        Guid sessionId, ScriptRunRequest request, CancellationToken ct)
    {
        var interpreter = request.Interpreter ?? "/bin/sh";
        var args        = new[] { request.ScriptPath }.Concat(request.Arguments).ToArray();
        var execReq     = new ExecRequest(
            Command:        $"{interpreter} {request.ScriptPath} {string.Join(' ', request.Arguments)}",
            WorkingDir:     null,
            Env:            request.Env,
            TimeoutSeconds: request.TimeoutSeconds,
            CaptureOutput:  true);

        return ExecuteAsync(sessionId, execReq, ct);
    }

    /// <inheritdoc/>
    public Task CancelAsync(Guid commandId, CancellationToken ct)
    {
        if (_commands.TryRemove(commandId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogInformation("Command {CommandId} cancelled", commandId);
        }
        return Task.CompletedTask;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RunCommandAsync(
        ShellSession session,
        Guid commandId,
        ExecRequest request,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var timeoutCts  = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts   = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        _commands[commandId]  = linkedCts;

        var sessionId = session.Block.Id;

        try
        {
            var env = request.Env?.ToDictionary() ?? [];
            var opts = new PtySpawnOptions(
                Executable:       session.Block.Shell,
                Arguments:        ["-c", request.Command],
                WorkingDirectory: request.WorkingDir ?? session.Block.WorkingDirectory,
                Environment:      env);

            var handle = await _pty.SpawnAsync(opts, linkedCts.Token).ConfigureAwait(false);

            if (_sessions is SessionManager sm)
                sm.AttachPtyHandle(sessionId, handle);

            await (_sessions as SessionManager)?.TransitionStateAsync(
                sessionId, ExecutionBlockState.Running, null, linkedCts.Token)!;

            if (request.CaptureOutput)
            {
                await foreach (var chunk in _pty.ReadOutputAsync(handle, sessionId, linkedCts.Token)
                                               .ConfigureAwait(false))
                {
                    await _broadcaster.BroadcastChunkAsync(chunk, linkedCts.Token)
                                      .ConfigureAwait(false);
                }
            }

            var exitCode = await _pty.WaitForExitAsync(handle, linkedCts.Token).ConfigureAwait(false);
            var newState  = exitCode == 0 ? ExecutionBlockState.Completed : ExecutionBlockState.Error;

            await (_sessions as SessionManager)?.TransitionStateAsync(
                sessionId, newState, exitCode, CancellationToken.None)!;

            var cfg = _config.Value;
            if (cfg.AuditEnabled)
            {
                var duration = DateTimeOffset.UtcNow - DateTimeOffset.UtcNow; // placeholder — computed below
                await _audit.LogCommandEndAsync(
                    commandId, exitCode, DateTimeOffset.UtcNow - session.Block.StartedAt.GetValueOrDefault(DateTimeOffset.UtcNow),
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Command {CommandId} in session {SessionId} was cancelled/timed-out",
                commandId, sessionId);
            await (_sessions as SessionManager)?.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandId} in session {SessionId} failed",
                commandId, sessionId);
            await (_sessions as SessionManager)?.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None)!;
        }
        finally
        {
            _commands.TryRemove(commandId, out _);
        }
    }
}
