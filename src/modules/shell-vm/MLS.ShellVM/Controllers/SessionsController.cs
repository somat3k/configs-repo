using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.ShellVM.Hubs;
using StackExchange.Redis;

namespace MLS.ShellVM.Controllers;

/// <summary>
/// HTTP REST API for shell session management.
/// Base path: <c>/api/sessions</c>.
/// </summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    ISessionManager _sessions,
    IExecutionEngine _engine,
    IAuditLogger _audit,
    IHubContext<ShellVMHub> _hub,
    IConnectionMultiplexer? _redis,
    ILogger<SessionsController> _logger) : ControllerBase
{
    /// <summary>POST /api/sessions — creates a new shell session.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return BadRequest(new { error = "label is required" });

        ShellSession session;
        try
        {
            session = await _sessions.CreateSessionAsync(request, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }

        var payload = new ShellSessionCreatedPayload(
            Label:             session.Block.Label,
            RequestingModuleId: session.Block.RequestingModuleId ?? ShellVMNetworkConstants.ModuleName,
            Timestamp:         DateTimeOffset.UtcNow.ToString("O"));

        var envelope = EnvelopePayload.Create(
            MessageTypes.ShellSessionCreated, ShellVMNetworkConstants.ModuleName, payload);

        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveSessionCreated", envelope, ct)
                  .ConfigureAwait(false);

        _logger.LogInformation("Session created: {Id} ({Label})",
            session.Block.Id, session.Block.Label);

        return CreatedAtAction(nameof(GetSession),
            new { id = session.Block.Id }, MapSession(session));
    }

    /// <summary>DELETE /api/sessions/{id} — terminates an active session.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> TerminateSession(Guid id, CancellationToken ct)
    {
        var session = await _sessions.GetSessionAsync(id, ct).ConfigureAwait(false);
        if (session is null)
            return NotFound(new { error = $"Session {id} not found" });

        var startedAt = session.Block.StartedAt ?? session.Block.CreatedAt;
        await _sessions.TerminateSessionAsync(id, graceful: true, ct).ConfigureAwait(false);

        var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        var payload = new ShellSessionTerminatedPayload(
            Label:        session.Block.Label,
            ExitCode:     session.Block.ExitCode,
            DurationMs:   durationMs,
            TerminatedBy: "client",
            Timestamp:    DateTimeOffset.UtcNow.ToString("O"));

        var envelope = EnvelopePayload.Create(
            MessageTypes.ShellSessionTerminated, ShellVMNetworkConstants.ModuleName, payload);

        await _hub.Clients.Group("broadcast")
                  .SendAsync("ReceiveSessionTerminated", envelope, ct)
                  .ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>GET /api/sessions — lists all active sessions.</summary>
    [HttpGet]
    public async IAsyncEnumerable<object> ListSessions(
        [FromQuery] string? state,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ExecutionBlockState? filter = null;
        if (!string.IsNullOrWhiteSpace(state) &&
            Enum.TryParse<ExecutionBlockState>(state, ignoreCase: true, out var parsed))
        {
            filter = parsed;
        }

        await foreach (var session in _sessions.GetSessionsAsync(filter, ct).ConfigureAwait(false))
        {
            yield return MapSession(session);
        }
    }

    /// <summary>GET /api/sessions/{id} — gets session details and current state.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var session = await _sessions.GetSessionAsync(id, ct).ConfigureAwait(false);
        return session is null
            ? NotFound(new { error = $"Session {id} not found" })
            : Ok(MapSession(session));
    }

    /// <summary>POST /api/sessions/{id}/exec — executes a command in a session.</summary>
    [HttpPost("{id:guid}/exec")]
    public async Task<IActionResult> ExecCommand(Guid id,
        [FromBody] ExecRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
            return BadRequest(new { error = "command is required" });

        var session = await _sessions.GetSessionAsync(id, ct).ConfigureAwait(false);
        if (session is null)
            return NotFound(new { error = $"Session {id} not found" });

        CommandExecution execution;
        try
        {
            execution = await _engine.ExecuteAsync(id, request, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }

        return Accepted(new
        {
            command_id = execution.Id,
            session_id = execution.SessionId,
            command    = execution.Command,
            state      = execution.State.ToString(),
            started_at = execution.StartedAt,
        });
    }

    /// <summary>POST /api/sessions/{id}/resize — resizes the PTY (cols × rows).</summary>
    [HttpPost("{id:guid}/resize")]
    public async Task<IActionResult> ResizePty(Guid id,
        [FromBody] ResizeRequest request, CancellationToken ct)
    {
        var session = await _sessions.GetSessionAsync(id, ct).ConfigureAwait(false);
        if (session is null)
            return NotFound(new { error = $"Session {id} not found" });

        _logger.LogInformation("Resize session {Id} → {Cols}×{Rows}", id, request.Cols, request.Rows);
        return Accepted(new { cols = request.Cols, rows = request.Rows });
    }

    /// <summary>GET /api/sessions/{id}/output — returns buffered output from the Redis ring-buffer.</summary>
    [HttpGet("{id:guid}/output")]
    public async Task<IActionResult> GetOutput(Guid id,
        [FromQuery] int lines = 100, CancellationToken ct = default)
    {
        if (_redis is null)
            return StatusCode(503, new { error = "Redis not available — output buffer unavailable" });

        var key = $"{ShellVMLimits.RedisOutputPrefix}{id}";
        var db  = _redis.GetDatabase();
        var raw = await db.ListRangeAsync(key, -lines, -1).ConfigureAwait(false);

        var output = raw
            .Select(r => JsonSerializer.Deserialize<ShellOutputPayload>((string)r!))
            .Where(p => p is not null)
            .ToArray();

        return Ok(new { session_id = id, count = output.Length, lines = output });
    }

    /// <summary>GET /api/sessions/{id}/audit — returns audit log entries for a session.</summary>
    [HttpGet("{id:guid}/audit")]
    public async IAsyncEnumerable<object> GetAudit(Guid id,
        [FromQuery] int limit = 50,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = new AuditQuery(SessionId: id, Limit: limit);
        await foreach (var entry in _audit.QueryAsync(query, ct).ConfigureAwait(false))
        {
            yield return new
            {
                id          = entry.Id,
                block_id    = entry.BlockId,
                command     = entry.Command,
                started_at  = entry.StartedAt,
                ended_at    = entry.EndedAt,
                exit_code   = entry.ExitCode,
                duration_ms = entry.DurationMs,
                module_id   = entry.ModuleId,
            };
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static object MapSession(ShellSession s) => new
    {
        id                  = s.Block.Id,
        label               = s.Block.Label,
        state               = s.Block.State.ToString(),
        shell               = s.Block.Shell,
        working_directory   = s.Block.WorkingDirectory,
        requesting_module_id= s.Block.RequestingModuleId,
        created_at          = s.Block.CreatedAt,
        started_at          = s.Block.StartedAt,
        completed_at        = s.Block.CompletedAt,
        exit_code           = s.Block.ExitCode,
        pty_attached        = s.PtyHandle is not null,
    };
}
