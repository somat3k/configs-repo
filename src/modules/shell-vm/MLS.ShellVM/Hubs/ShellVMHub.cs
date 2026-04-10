using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.ShellVM.Hubs;

/// <summary>
/// SignalR hub exposing the shell-vm WebSocket API on port 6950.
/// Handles command execution requests, PTY stdin input, terminal resize events,
/// and real-time output streaming to subscribers.
/// </summary>
/// <remarks>
/// Connection groups:
/// <list type="bullet">
///   <item><description><c>session:{id}</c> — connections subscribed to a specific session's output.</description></item>
///   <item><description><c>broadcast</c> — all connected clients (session lifecycle events).</description></item>
/// </list>
/// </remarks>
public sealed class ShellVMHub(
    ISessionManager _sessions,
    IExecutionEngine _engine,
    IPtyProvider _pty,
    IOutputBroadcaster _broadcaster,
    ILogger<ShellVMHub> _logger) : Hub<IShellVMHubClient>
{
    /// <summary>
    /// Executes a command in the session identified by <c>Envelope.SessionId</c>.
    /// </summary>
    public async Task ExecCommand(EnvelopePayload envelope)
    {
        var sessionId = envelope.SessionId;
        var request   = JsonSerializer.Deserialize<ShellExecRequestPayload>(envelope.Payload.GetRawText());
        if (request is null)
        {
            _logger.LogWarning("ExecCommand: null payload from connection {ConnId}", Context.ConnectionId);
            return;
        }

        var execReq = new ExecRequest(
            Command:        request.Command,
            WorkingDir:     request.WorkingDir,
            Env:            request.Env,
            TimeoutSeconds: request.TimeoutSeconds,
            CaptureOutput:  request.CaptureOutput);

        try
        {
            await _engine.ExecuteAsync(sessionId, execReq, Context.ConnectionAborted)
                         .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ExecCommand failed for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Forwards raw stdin bytes to the PTY of the session identified by <c>Envelope.SessionId</c>.
    /// </summary>
    public async Task SendInput(EnvelopePayload envelope)
    {
        var sessionId = envelope.SessionId;
        var payload   = JsonSerializer.Deserialize<ShellInputPayload>(envelope.Payload.GetRawText());
        if (payload is null) return;

        var session = await _sessions.GetSessionAsync(sessionId, Context.ConnectionAborted)
                                     .ConfigureAwait(false);
        if (session?.PtyHandle is null)
        {
            _logger.LogWarning("SendInput: no active PTY for session {SessionId}", sessionId);
            return;
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(payload.Data);
        await _pty.WriteInputAsync(session.PtyHandle, bytes, Context.ConnectionAborted)
                  .ConfigureAwait(false);
    }

    /// <summary>
    /// Resizes the PTY of the session identified by <c>Envelope.SessionId</c>.
    /// </summary>
    public async Task ResizePty(EnvelopePayload envelope)
    {
        var sessionId = envelope.SessionId;
        var payload   = JsonSerializer.Deserialize<ShellResizePayload>(envelope.Payload.GetRawText());
        if (payload is null) return;

        _logger.LogDebug("ResizePty session={SessionId} {Cols}×{Rows}",
            sessionId, payload.Cols, payload.Rows);
        // Actual resize is a no-op in managed mode (logged in PtyProviderService)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes the caller's SignalR connection to a session's output stream.
    /// </summary>
    public async Task SubscribeSession(Guid sessionId)
    {
        await _broadcaster.SubscribeAsync(Context.ConnectionId, sessionId, Context.ConnectionAborted)
                          .ConfigureAwait(false);
        _logger.LogDebug("Connection {ConnId} subscribed to session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// Unsubscribes the caller's SignalR connection from all session output streams.
    /// </summary>
    public async Task UnsubscribeSession(Guid sessionId)
    {
        await _broadcaster.UnsubscribeAsync(Context.ConnectionId, Context.ConnectionAborted)
                          .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var http     = Context.GetHttpContext();
        var moduleId = http?.Request.Query["moduleId"].ToString();
        var clientId = http?.Request.Query["clientId"].ToString();

        var peerId = !string.IsNullOrWhiteSpace(moduleId) ? moduleId
                   : !string.IsNullOrWhiteSpace(clientId) ? clientId
                   : null;

        if (peerId is not null)
        {
            var safeGroup = SanitisePeerId(peerId);
            await Groups.AddToGroupAsync(Context.ConnectionId, safeGroup)
                        .ConfigureAwait(false);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        await _broadcaster.UnsubscribeAsync(Context.ConnectionId, CancellationToken.None)
                          .ConfigureAwait(false);
        await base.OnDisconnectedAsync(ex).ConfigureAwait(false);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string SanitisePeerId(string id) =>
        (id.Length > 64 ? id[..64] : id)
            .Replace('\r', '_')
            .Replace('\n', '_');
}

/// <summary>Client-side methods pushed by the Shell VM hub.</summary>
public interface IShellVMHubClient
{
    /// <summary>Receives a stdout/stderr output chunk from a running session.</summary>
    Task ReceiveOutput(EnvelopePayload envelope);

    /// <summary>Receives a session state change notification.</summary>
    Task ReceiveSessionState(EnvelopePayload envelope);

    /// <summary>Receives a new-session creation broadcast.</summary>
    Task ReceiveSessionCreated(EnvelopePayload envelope);

    /// <summary>Receives a session termination broadcast.</summary>
    Task ReceiveSessionTerminated(EnvelopePayload envelope);

    /// <summary>Receives a MODULE_HEARTBEAT forwarded from the hub.</summary>
    Task ReceiveHeartbeat(EnvelopePayload envelope);
}
