namespace MLS.ShellVM.Payloads;

/// <summary>
/// Request to execute a command in an existing shell session.
/// The target session is identified by <c>Envelope.SessionId</c>.
/// </summary>
/// <param name="Command">Shell command string to execute.</param>
/// <param name="WorkingDir">Override working directory for this command.</param>
/// <param name="Env">Additional environment variables for this execution.</param>
/// <param name="TimeoutSeconds">Maximum allowed execution time.</param>
/// <param name="CaptureOutput">When <see langword="true"/>, stdout/stderr is streamed to subscribers.</param>
public sealed record ShellExecRequestPayload(
    string Command,
    string WorkingDir,
    IReadOnlyDictionary<string, string>? Env,
    int TimeoutSeconds = 300,
    bool CaptureOutput = true);

/// <summary>
/// Raw stdin bytes sent to an interactive PTY session.
/// The target session is identified by <c>Envelope.SessionId</c>.
/// </summary>
/// <param name="Data">UTF-8 input text or control sequences.</param>
public sealed record ShellInputPayload(string Data);

/// <summary>
/// PTY resize request.
/// The target session is identified by <c>Envelope.SessionId</c>.
/// </summary>
/// <param name="Cols">New column count.</param>
/// <param name="Rows">New row count.</param>
public sealed record ShellResizePayload(int Cols, int Rows);

/// <summary>
/// A chunk of PTY output streamed to subscribers.
/// The originating session is identified by <c>Envelope.SessionId</c>.
/// </summary>
/// <param name="Stream">The output stream: <c>"stdout"</c> or <c>"stderr"</c>.</param>
/// <param name="Chunk">UTF-8 decoded output content.</param>
/// <param name="Sequence">Monotonically increasing sequence number per session.</param>
/// <param name="Timestamp">ISO 8601 UTC production timestamp.</param>
public sealed record ShellOutputPayload(
    string Stream,
    string Chunk,
    long Sequence,
    string Timestamp);

/// <summary>
/// State change notification for an execution block.
/// The affected session is identified by <c>Envelope.SessionId</c>.
/// </summary>
/// <param name="PreviousState">The state before the transition.</param>
/// <param name="CurrentState">The state after the transition.</param>
/// <param name="ExitCode">Process exit code; <see langword="null"/> for non-terminal transitions.</param>
/// <param name="DurationMs">Total session duration in milliseconds from <c>StartedAt</c> to now.</param>
public sealed record ShellSessionStatePayload(
    string PreviousState,
    string CurrentState,
    int? ExitCode,
    long DurationMs);

/// <summary>Broadcast when a new shell session is created.</summary>
/// <param name="Label">Human-readable session label.</param>
/// <param name="RequestingModuleId">Module ID of the entity that created the session.</param>
/// <param name="Timestamp">ISO 8601 UTC creation timestamp.</param>
public sealed record ShellSessionCreatedPayload(
    string Label,
    string RequestingModuleId,
    string Timestamp);

/// <summary>Broadcast when a shell session terminates.</summary>
/// <param name="Label">Human-readable session label.</param>
/// <param name="ExitCode">Final process exit code; <see langword="null"/> on forced termination.</param>
/// <param name="DurationMs">Total session duration in milliseconds.</param>
/// <param name="TerminatedBy">Termination actor: <c>"client"</c>, <c>"watchdog"</c>, <c>"timeout"</c>, or <c>"error"</c>.</param>
/// <param name="Timestamp">ISO 8601 UTC termination timestamp.</param>
public sealed record ShellSessionTerminatedPayload(
    string Label,
    int? ExitCode,
    long DurationMs,
    string TerminatedBy,
    string Timestamp);
