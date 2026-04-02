# shell-vm — Session 3: Required Interfaces

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

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
