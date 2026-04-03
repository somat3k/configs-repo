# Shell VM Module — Hyper-Detailed Copilot Session Prompt

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 1. Module Identity

| Field | Value |
|---|---|
| **Name** | `shell-vm` |
| **Namespace** | `MLS.ShellVM` |
| **Role** | Sandboxed shell execution engine and operator execution console |
| **HTTP Port** | `5950` |
| **WebSocket Port** | `6950` |
| **Container** | `mls-shell-vm` |
| **Docker image** | `ghcr.io/somat3k/mls-shell-vm:latest` |
| **Network** | `mls-network` (bridge, same as all modules) |

---

## 2. Architecture Source — WaveTerm Concepts Applied

This module is architecturally inspired by [WaveTerm](https://github.com/wavetermdev/waveterm):

| WaveTerm Concept | MLS Shell VM Equivalent |
|---|---|
| **Block** — independent terminal widget with unique ID | `ExecutionBlock` — PTY session or command run with UUID |
| **Durable SSH session** — survives reconnects | `ISessionManager` + Redis registry persists sessions across WS drops |
| **WSH protocol** — shell-to-shell data sharing | `SHELL_EXEC_REQUEST` envelope — any module can trigger execution |
| **Command Blocks** — isolated individual command tracking | `CommandExecution` — each command gets its own audit entry |
| **PTY streaming** — real-time stdout/stderr | `IOutputBroadcaster` — `Channel<OutputChunk>` fan-out via SignalR |
| **Block state machine** | `ExecutionBlockState` enum with transitions |

---

## 3. Required Interfaces

All interfaces use primary constructor injection. XML documentation is required on every member.

```csharp
namespace MLS.ShellVM.Interfaces;

/// <summary>Manages the lifecycle of shell execution sessions.</summary>
public interface ISessionManager
{
    /// <summary>Creates a new PTY-backed shell session.</summary>
    Task<ShellSession> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct);

    /// <summary>Terminates an active session by force or graceful signal.</summary>
    Task TerminateSessionAsync(Guid sessionId, bool graceful, CancellationToken ct);

    /// <summary>Retrieves a session by its unique ID.</summary>
    Task<ShellSession?> GetSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>Lists all active sessions with optional status filter.</summary>
    IAsyncEnumerable<ShellSession> GetSessionsAsync(ExecutionBlockState? filter, CancellationToken ct);

    /// <summary>Persists session state to Redis for reconnect durability.</summary>
    Task PersistSessionStateAsync(Guid sessionId, CancellationToken ct);
}

/// <summary>Provides a platform abstraction for pseudo-terminal (PTY) management.</summary>
public interface IPtyProvider
{
    /// <summary>Spawns a PTY-attached process with the given parameters.</summary>
    Task<PtyHandle> SpawnAsync(PtySpawnOptions options, CancellationToken ct);

    /// <summary>Writes raw bytes to the PTY's stdin.</summary>
    ValueTask WriteInputAsync(PtyHandle handle, ReadOnlyMemory<byte> data, CancellationToken ct);

    /// <summary>Reads the next output chunk from the PTY's stdout/stderr.</summary>
    IAsyncEnumerable<OutputChunk> ReadOutputAsync(PtyHandle handle, CancellationToken ct);

    /// <summary>Resizes the PTY to the specified dimensions.</summary>
    ValueTask ResizeAsync(PtyHandle handle, int cols, int rows, CancellationToken ct);

    /// <summary>Kills the underlying process and releases all PTY resources.</summary>
    ValueTask KillAsync(PtyHandle handle, CancellationToken ct);
}

/// <summary>Executes commands and scripts in a sandboxed environment.</summary>
public interface IExecutionEngine
{
    /// <summary>Executes a shell command inside an existing session.</summary>
    Task<CommandExecution> ExecuteAsync(Guid sessionId, ExecRequest request, CancellationToken ct);

    /// <summary>Executes a named script with environment isolation.</summary>
    Task<CommandExecution> RunScriptAsync(Guid sessionId, ScriptRunRequest request, CancellationToken ct);

    /// <summary>Cancels a running command (sends SIGTERM, then SIGKILL after grace period).</summary>
    Task CancelAsync(Guid commandId, CancellationToken ct);
}

/// <summary>Writes structured audit records for all shell activity.</summary>
public interface IAuditLogger
{
    /// <summary>Logs a command execution attempt (before execution).</summary>
    Task LogCommandStartAsync(AuditEntry entry, CancellationToken ct);

    /// <summary>Logs the outcome of a completed command.</summary>
    Task LogCommandEndAsync(Guid commandId, int exitCode, TimeSpan duration, CancellationToken ct);

    /// <summary>Queries audit records for a given session.</summary>
    IAsyncEnumerable<AuditEntry> QueryAsync(AuditQuery query, CancellationToken ct);
}

/// <summary>Broadcasts real-time output chunks to WebSocket subscribers.</summary>
public interface IOutputBroadcaster
{
    /// <summary>Enqueues an output chunk for fan-out delivery to all session subscribers.</summary>
    ValueTask BroadcastChunkAsync(OutputChunk chunk, CancellationToken ct);

    /// <summary>Subscribes a SignalR connection to a session's output stream.</summary>
    Task SubscribeAsync(string connectionId, Guid sessionId, CancellationToken ct);

    /// <summary>Unsubscribes a connection from a session's output stream.</summary>
    Task UnsubscribeAsync(string connectionId, CancellationToken ct);
}
```

---

## 4. Core Models

```csharp
namespace MLS.ShellVM.Models;

/// <summary>Represents a persistent, PTY-backed execution session (EF Core entity).</summary>
public class ExecutionBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;          // Human-readable name
    public ExecutionBlockState State { get; set; } = ExecutionBlockState.Created;
    public string Shell { get; set; } = "/bin/sh";              // or "python3", "ape"
    public string WorkingDirectory { get; set; } = "/app";
    public Dictionary<string, string> Environment { get; set; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? ExitCode { get; set; }
    public string? RequestingModuleId { get; set; }             // Which module triggered this
}

/// <summary>In-memory session wrapper around an ExecutionBlock with its live PTY handle.</summary>
public sealed record ShellSession(
    ExecutionBlock Block,
    PtyHandle? PtyHandle,
    Channel<OutputChunk> OutputChannel
);

/// <summary>State machine for execution blocks.</summary>
public enum ExecutionBlockState
{
    Created,
    Starting,
    Running,
    Paused,
    Completed,
    Error,
    Terminated
}

/// <summary>A single chunk of output from a PTY session.</summary>
public sealed record OutputChunk(
    Guid SessionId,
    OutputStream Stream,        // Stdout or Stderr
    string Data,                // UTF-8 decoded content
    long Sequence,              // Monotonically increasing per session
    DateTimeOffset Timestamp
);

public enum OutputStream { Stdout, Stderr }

/// <summary>Tracks a single command execution within a session.</summary>
public sealed record CommandExecution(
    Guid Id,
    Guid SessionId,
    string Command,
    CommandState State,
    DateTimeOffset StartedAt
);

public enum CommandState { Pending, Running, Completed, Cancelled, Failed }

/// <summary>Handle to an active PTY-attached process.</summary>
public sealed record PtyHandle(
    int ProcessId,
    string ProcessName,
    int Cols,
    int Rows
);

/// <summary>Options used when spawning a new PTY process.</summary>
public sealed record PtySpawnOptions(
    string Executable,
    string[] Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    int Cols = 220,
    int Rows = 50
);
```

---

## 5. Payload Records (Envelope Payload Bodies)

> **Note**: `Envelope.SessionId` carries the shell session identifier for all `SHELL_*` messages.
> Do **not** repeat it inside the payload body — the records below omit it intentionally.

```csharp
namespace MLS.ShellVM.Payloads;

/// <summary>Request to execute a command in an existing shell session.
/// The target session is identified by Envelope.SessionId.</summary>
public sealed record ShellExecRequestPayload(
    string Command,
    string WorkingDir,
    IReadOnlyDictionary<string, string>? Env,
    int TimeoutSeconds = 300,
    bool CaptureOutput = true
);

/// <summary>Raw stdin bytes sent to an interactive PTY session.
/// The target session is identified by Envelope.SessionId.</summary>
public sealed record ShellInputPayload(
    string Data    // UTF-8 input text or control sequences
);

/// <summary>PTY resize request.
/// The target session is identified by Envelope.SessionId.</summary>
public sealed record ShellResizePayload(
    int Cols,
    int Rows
);

/// <summary>A chunk of PTY output streamed to subscribers.
/// The originating session is identified by Envelope.SessionId.</summary>
public sealed record ShellOutputPayload(
    string Stream,     // "stdout" or "stderr"
    string Chunk,      // UTF-8 decoded output
    long Sequence,
    string Timestamp   // ISO 8601
);

/// <summary>State change notification for an execution block.
/// The affected session is identified by Envelope.SessionId.</summary>
public sealed record ShellSessionStatePayload(
    string PreviousState,
    string CurrentState,
    int? ExitCode,
    long DurationMs
);

/// <summary>Broadcast when a new session is created.</summary>
public sealed record ShellSessionCreatedPayload(
    string Label,
    string RequestingModuleId,
    string Timestamp
);

/// <summary>Broadcast when a session terminates (normal exit, timeout, or watchdog reap).</summary>
public sealed record ShellSessionTerminatedPayload(
    string Label,
    int? ExitCode,
    long DurationMs,
    string TerminatedBy,   // "client" | "watchdog" | "timeout" | "error"
    string Timestamp
);
```

---

## 6. SignalR Hub

```csharp
namespace MLS.ShellVM.Hubs;

/// <summary>
/// SignalR hub exposing shell-vm WebSocket API on port 6950.
/// Handles command execution requests, PTY input, resize events,
/// and real-time output streaming.
/// </summary>
public sealed class ShellVMHub(
    ISessionManager _sessions,
    IExecutionEngine _engine,
    IOutputBroadcaster _broadcaster,
    ILogger<ShellVMHub> _logger
) : Hub<IShellVMHubClient>
{
    public async Task ExecCommand(EnvelopePayload envelope) { /* dispatch to engine */ }
    public async Task SendInput(EnvelopePayload envelope)   { /* forward bytes to PTY */ }
    public async Task ResizePty(EnvelopePayload envelope)   { /* update cols×rows */ }
    public async Task SubscribeSession(Guid sessionId)      { /* join SignalR group */ }
    public async Task UnsubscribeSession(Guid sessionId)    { /* leave SignalR group */ }
    public override async Task OnDisconnectedAsync(Exception? ex)  { /* auto-unsubscribe */ }
}

/// <summary>Client-side methods pushed by the hub.</summary>
public interface IShellVMHubClient
{
    Task ReceiveOutput(EnvelopePayload envelope);
    Task ReceiveSessionState(EnvelopePayload envelope);
    Task ReceiveSessionCreated(EnvelopePayload envelope);
    Task ReceiveSessionTerminated(EnvelopePayload envelope);
    Task ReceiveHeartbeat(EnvelopePayload envelope);
}
```

---

## 7. REST Controller Pattern

```csharp
namespace MLS.ShellVM.Controllers;

/// <summary>HTTP API for shell session management. All endpoints return typed responses.</summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    ISessionManager _sessions,
    IExecutionEngine _engine
) : ControllerBase
{
    [HttpPost]        // POST /api/sessions
    [HttpDelete("{id:guid}")]
    [HttpGet]
    [HttpGet("{id:guid}")]
    [HttpPost("{id:guid}/exec")]
    [HttpPost("{id:guid}/resize")]
    [HttpGet("{id:guid}/output")]
}
```

---

## 8. Background Services

```csharp
namespace MLS.ShellVM.Background;

/// <summary>
/// Sends MODULE_HEARTBEAT to Block Controller every 5 seconds.
/// Includes active session count, CPU %, memory MB, and commands processed.
/// </summary>
public sealed class HeartbeatService(
    IHttpClientFactory _http,
    ISessionManager _sessions,
    IModuleIdentity _identity,
    ILogger<HeartbeatService> _logger
) : BackgroundService { ... }

/// <summary>
/// Monitors sessions for timeout violations and reaps stale PTY processes.
/// Runs every 30 seconds; configurable max idle time via ShellVMConfig.
/// </summary>
public sealed class SessionWatchdog(
    ISessionManager _sessions,
    ShellVMConfig _config,
    ILogger<SessionWatchdog> _logger
) : BackgroundService { ... }
```

---

## 9. Configuration

```json
// appsettings.json
{
  "MLS": {
    "Module": "shell-vm",
    "HttpPort": 5950,
    "WsPort": 6950,
    "Network": {
      "BlockControllerUrl": "http://block-controller:5100",
      "RedisUrl": "redis:6379",
      "PostgresConnectionString": "Host=postgres;Port=5432;Database=mls_db;Username=mls_user"
    }
  },
  "ShellVM": {
    "MaxConcurrentSessions": 32,
    "MaxIdleSessionSeconds": 1800,
    "DefaultShell": "/bin/sh",
    "AllowedShells": ["/bin/sh", "/bin/bash", "python3", "ape"],
    "OutputRingBufferLines": 10000,
    "CommandTimeoutSeconds": 600,
    "AuditEnabled": true,
    "SandboxCpuPercent": 80,
    "SandboxMemoryMb": 2048
  }
}
```

```csharp
namespace MLS.ShellVM.Models;

/// <summary>Strongly-typed configuration bound from appsettings.json ShellVM section.</summary>
public sealed class ShellVMConfig
{
    public int MaxConcurrentSessions { get; init; } = 32;
    public int MaxIdleSessionSeconds { get; init; } = 1800;
    public string DefaultShell { get; init; } = "/bin/sh";
    public string[] AllowedShells { get; init; } = ["/bin/sh", "/bin/bash", "python3", "ape"];
    public int OutputRingBufferLines { get; init; } = 10_000;
    public int CommandTimeoutSeconds { get; init; } = 600;
    public bool AuditEnabled { get; init; } = true;
    public int SandboxCpuPercent { get; init; } = 80;
    public int SandboxMemoryMb { get; init; } = 2048;
}
```

---

## 10. Constants

```csharp
namespace MLS.ShellVM.Constants;

/// <summary>All shell-vm message type constants for the Envelope Protocol.</summary>
public static class ShellVMMessageTypes
{
    public const string ExecRequest      = "SHELL_EXEC_REQUEST";
    public const string Input            = "SHELL_INPUT";
    public const string Resize           = "SHELL_RESIZE";
    public const string Output           = "SHELL_OUTPUT";
    public const string SessionState     = "SHELL_SESSION_STATE";
    public const string SessionCreated   = "SHELL_SESSION_CREATED";
    public const string SessionTerminated = "SHELL_SESSION_TERMINATED";
}

/// <summary>Network and port constants for the shell-vm module.</summary>
public static class ShellVMNetworkConstants
{
    public const int HttpPort = 5950;
    public const int WsPort   = 6950;
    public const string ModuleName = "shell-vm";
    public const string ContainerName = "mls-shell-vm";
    public const string DockerImage = "ghcr.io/somat3k/mls-shell-vm:latest";
}
```

---

## 11. Message Flow Diagrams

### 11.1 Module-Triggered Script Execution

```
ML Runtime → BC (ROUTE_MESSAGE: SHELL_EXEC_REQUEST)
           → BC routes → ShellVM HTTP POST /api/sessions/{id}/exec
           ← ShellVM responds with CommandExecution (202 Accepted)
           
ShellVM → Channel<OutputChunk> (internal fan-out)
       → SignalR group: "session:{id}"
       → Web App ← receives SHELL_OUTPUT in real-time

ShellVM → BC (SHELL_SESSION_STATE: Completed, exit_code: 0)
```

### 11.2 Interactive Console (Operator via Web App)

```
Web App → ShellVM WS (SHELL_EXEC_REQUEST: session_id, cmd="/bin/bash")
ShellVM creates ExecutionBlock → state: Starting → Running
ShellVM → Web App (SHELL_SESSION_CREATED)

Operator types → Web App → ShellVM WS (SHELL_INPUT: data="ls -la\n")
ShellVM PTY stdin ← data
PTY stdout → ShellVM OutputBroadcaster → Web App (SHELL_OUTPUT chunks)

Operator exits → Web App → ShellVM WS (terminate)
ShellVM → Web App (SHELL_SESSION_STATE: Terminated)
ShellVM → BC (MODULE_HEARTBEAT: active_sessions -1)
```

### 11.3 Block Controller Heartbeat

```
HeartbeatService (every 5s):
  ShellVM → BC HTTP POST /api/modules/{id}/heartbeat
  payload: {
    status: "healthy",
    uptime_seconds: 3600,
    metrics: { active_sessions: 3, commands_executed: 142, cpu_percent: 18.4, memory_mb: 312 }
  }
```

---

## 12. Database Schema

```sql
-- Enable pgcrypto for gen_random_uuid() (idempotent, safe to re-run)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Persistent execution block registry
CREATE TABLE execution_blocks (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    label               TEXT NOT NULL,
    state               TEXT NOT NULL,
    shell               TEXT NOT NULL DEFAULT '/bin/sh',
    working_directory   TEXT NOT NULL DEFAULT '/app',
    environment         JSONB,
    requesting_module_id TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    exit_code           INT
);

-- Full audit log — never truncated; partition by month.
-- A DEFAULT partition is required so inserts succeed before month-specific
-- partitions are created by the monthly maintenance job.
CREATE TABLE shell_audit_log (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    block_id    UUID REFERENCES execution_blocks(id),
    command     TEXT NOT NULL,
    started_at  TIMESTAMPTZ NOT NULL,
    ended_at    TIMESTAMPTZ,
    exit_code   INT,
    duration_ms BIGINT,
    module_id   TEXT
) PARTITION BY RANGE (started_at);

-- Default partition catches rows that don't match any month partition yet.
-- This partition can remain indefinitely as a safety catch-all, or data can be
-- migrated to month-specific partitions before it is dropped (PostgreSQL requires
-- the partition to be empty before it can be removed).
CREATE TABLE shell_audit_log_default PARTITION OF shell_audit_log DEFAULT;
```

---

## 13. Skills to Apply

| Skill | Why |
|---|---|
| `.skills/dotnet-devs.md` | C# 13, primary constructors, async patterns, DI |
| `.skills/networking.md` | WebSocket server, Block Controller registration, heartbeat |
| `.skills/websockets-inferences.md` | SignalR hub, `Channel<T>` fan-out, envelope protocol |
| `.skills/beast-development.md` | High-throughput output streaming, object pooling for output chunks |
| `.skills/storage-data-management.md` | EF Core for `execution_blocks`, Redis ring-buffer, audit log partitions |
| `.skills/agents.md` | Module agent lifecycle: Initialize → Start → Running → Stop |
| `.skills/system-architect.md` | Envelope protocol, Block Controller topology, port allocation |

---

## 14. Compliance Checklist

Before marking a code change complete, verify all items:

- [ ] Module registers with Block Controller on startup via `POST /api/modules/register`
- [ ] Heartbeat sent every 5 seconds with metrics via `HeartbeatService`
- [ ] All WS messages use `EnvelopePayload` with `Version >= 1` and `Type` from `ShellVMMessageTypes`
- [ ] `ISessionManager` persists session state to Redis on every state transition
- [ ] `IAuditLogger` writes every command start and end to `shell_audit_log`
- [ ] `SessionWatchdog` reaps idle sessions beyond `MaxIdleSessionSeconds`
- [ ] PTY processes are killed when session terminates (no zombie processes)
- [ ] `ShellVMConfig.AllowedShells` is enforced — reject any shell not in the allow-list
- [ ] `CommandTimeoutSeconds` enforced — `CancellationToken` propagated to PTY process
- [ ] All HTTP and WS ports declared as `ShellVMNetworkConstants` constants — no magic numbers
- [ ] XML docs on every public interface, record, and method
- [ ] xUnit tests cover session create/exec/terminate state machine
- [ ] xUnit tests cover WebSocket output streaming
- [ ] Dockerfile uses multi-stage build (see `.skills/multi-stage-dockerfile.md`)
- [ ] Docker service registered in `docker-compose.yml` with correct ports `5950:5950` / `6950:6950`
