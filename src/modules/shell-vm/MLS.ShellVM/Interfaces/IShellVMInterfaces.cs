namespace MLS.ShellVM.Interfaces;

/// <summary>Manages the lifecycle of shell execution sessions.</summary>
public interface ISessionManager
{
    /// <summary>Creates a new PTY-backed shell session and persists its initial state.</summary>
    Task<ShellSession> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct);

    /// <summary>Terminates an active session by force or graceful signal.</summary>
    Task TerminateSessionAsync(Guid sessionId, bool graceful, CancellationToken ct);

    /// <summary>Retrieves a live in-memory session by its unique ID.</summary>
    Task<ShellSession?> GetSessionAsync(Guid sessionId, CancellationToken ct);

    /// <summary>Lists all active in-memory sessions with an optional state filter.</summary>
    IAsyncEnumerable<ShellSession> GetSessionsAsync(ExecutionBlockState? filter, CancellationToken ct);

    /// <summary>
    /// Persists current session state to Redis so it survives WebSocket reconnections.
    /// </summary>
    Task PersistSessionStateAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// Transitions the session state machine to <paramref name="newState"/>, updates
    /// timestamps, and persists the change to both PostgreSQL and Redis.
    /// </summary>
    Task TransitionStateAsync(Guid sessionId, ExecutionBlockState newState, int? exitCode, CancellationToken ct);

    /// <summary>
    /// Replaces the live <see cref="PtyHandle"/> on an existing in-memory session record.
    /// </summary>
    void AttachPtyHandle(Guid sessionId, PtyHandle handle);

    /// <summary>
    /// Updates <see cref="ExecutionBlock.LastActivityAt"/> to the current UTC time.
    /// Called on every stdin write and every output broadcast to enable accurate idle detection.
    /// </summary>
    void UpdateLastActivity(Guid sessionId);

    /// <summary>Returns the current number of active (non-terminal) sessions.</summary>
    int ActiveSessionCount { get; }
}

/// <summary>Provides a platform abstraction for pseudo-terminal (PTY) management.</summary>
public interface IPtyProvider
{
    /// <summary>Spawns a PTY-attached process using the supplied options.</summary>
    Task<PtyHandle> SpawnAsync(PtySpawnOptions options, CancellationToken ct);

    /// <summary>Writes raw bytes to the PTY's stdin.</summary>
    ValueTask WriteInputAsync(PtyHandle handle, ReadOnlyMemory<byte> data, CancellationToken ct);

    /// <summary>
    /// Reads output chunks from the PTY's stdout/stderr until the process exits or
    /// <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<OutputChunk> ReadOutputAsync(PtyHandle handle, Guid sessionId, CancellationToken ct);

    /// <summary>Resizes the terminal to the specified dimensions.</summary>
    ValueTask ResizeAsync(PtyHandle handle, int cols, int rows, CancellationToken ct);

    /// <summary>Kills the underlying process and releases all PTY resources.</summary>
    ValueTask KillAsync(PtyHandle handle, CancellationToken ct);

    /// <summary>
    /// Waits for the process to exit and returns its exit code.
    /// </summary>
    Task<int> WaitForExitAsync(PtyHandle handle, CancellationToken ct);
}

/// <summary>Executes commands and scripts in a sandboxed shell session.</summary>
public interface IExecutionEngine
{
    /// <summary>Executes a shell command inside an existing session.</summary>
    Task<CommandExecution> ExecuteAsync(Guid sessionId, ExecRequest request, CancellationToken ct);

    /// <summary>Executes a named script with environment isolation inside an existing session.</summary>
    Task<CommandExecution> RunScriptAsync(Guid sessionId, ScriptRunRequest request, CancellationToken ct);

    /// <summary>
    /// Cancels a running command by killing the underlying PTY process.
    /// Sends SIGTERM via <see cref="IPtyProvider.KillAsync"/> and then cancels
    /// the command's <see cref="CancellationTokenSource"/> so streaming stops cleanly.
    /// </summary>
    Task CancelAsync(Guid commandId, CancellationToken ct);
}

/// <summary>Writes structured audit records for all shell activity.</summary>
public interface IAuditLogger
{
    /// <summary>Logs a command execution attempt immediately before the command is dispatched.</summary>
    Task LogCommandStartAsync(AuditEntry entry, CancellationToken ct);

    /// <summary>Logs the outcome of a completed command execution.</summary>
    Task LogCommandEndAsync(Guid commandId, int exitCode, TimeSpan duration, CancellationToken ct);

    /// <summary>Queries audit records matching the given criteria.</summary>
    IAsyncEnumerable<AuditEntry> QueryAsync(AuditQuery query, CancellationToken ct);
}

/// <summary>Broadcasts real-time output chunks to WebSocket subscribers.</summary>
public interface IOutputBroadcaster
{
    /// <summary>Enqueues an output chunk for fan-out delivery to all session subscribers.</summary>
    ValueTask BroadcastChunkAsync(OutputChunk chunk, CancellationToken ct);

    /// <summary>Subscribes a SignalR connection to a session's output stream.</summary>
    Task SubscribeAsync(string connectionId, Guid sessionId, CancellationToken ct);

    /// <summary>Removes all subscriptions held by a SignalR connection.</summary>
    Task UnsubscribeAsync(string connectionId, CancellationToken ct);

    /// <summary>Removes the subscription of a specific session from a SignalR connection.</summary>
    Task UnsubscribeAsync(string connectionId, Guid sessionId, CancellationToken ct);
}
