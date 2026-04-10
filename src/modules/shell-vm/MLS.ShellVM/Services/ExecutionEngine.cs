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
    // commandId → the CTS that can cancel that command.
    // The CTS is owned by RunCommandAsync; CancelAsync only signals it (no dispose).
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
        if (_commands.TryGetValue(commandId, out var cts))
        {
            // Only cancel — RunCommandAsync owns the CTS lifetime and disposes it
            cts.Cancel();
            _logger.LogInformation("Command {CommandId} cancellation requested", commandId);
        }
        return Task.CompletedTask;
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

        try
        {
            var env  = request.Env?.ToDictionary() ?? [];
            var opts = new PtySpawnOptions(
                Executable:       session.Block.Shell,
                Arguments:        ["-c", request.Command],
                WorkingDirectory: request.WorkingDir ?? session.Block.WorkingDirectory,
                Environment:      env);

            var handle = await _pty.SpawnAsync(opts, linkedCts.Token).ConfigureAwait(false);
            _sessions.AttachPtyHandle(sessionId, handle);

            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Running, null, linkedCts.Token).ConfigureAwait(false);

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
            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandId} in session {SessionId} failed",
                commandId, sessionId);
            await _sessions.TransitionStateAsync(
                sessionId, ExecutionBlockState.Error, -1, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _commands.TryRemove(commandId, out _);
        }
    }
}
