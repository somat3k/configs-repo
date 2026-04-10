using System.Threading.Channels;

namespace MLS.ShellVM.Models;

/// <summary>Represents a persistent, PTY-backed execution session stored in PostgreSQL.</summary>
public class ExecutionBlock
{
    /// <summary>Unique session identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Human-readable name for the session.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Current lifecycle state of the session.</summary>
    public ExecutionBlockState State { get; set; } = ExecutionBlockState.Created;

    /// <summary>Shell executable path or interpreter name (e.g. <c>/bin/sh</c>, <c>python3</c>).</summary>
    public string Shell { get; set; } = "/bin/sh";

    /// <summary>Working directory for the shell process.</summary>
    public string WorkingDirectory { get; set; } = "/app";

    /// <summary>Additional environment variables injected into the shell process.</summary>
    public Dictionary<string, string> Environment { get; set; } = [];

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the shell process transitioned to <see cref="ExecutionBlockState.Running"/>.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>UTC timestamp when the shell process reached a terminal state.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Process exit code; <see langword="null"/> while still running.</summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// UTC timestamp of the last recorded stdin/stdout/stderr activity.
    /// Updated on every input write and every output chunk broadcast.
    /// Used by <c>SessionWatchdog</c> to detect truly idle sessions.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Registered module ID of the entity that requested this session.</summary>
    public string? RequestingModuleId { get; set; }
}

/// <summary>State machine for execution blocks.</summary>
public enum ExecutionBlockState
{
    /// <summary>Session record has been created but the process has not started yet.</summary>
    Created,

    /// <summary>The shell process is being spawned.</summary>
    Starting,

    /// <summary>The shell process is active and accepting input.</summary>
    Running,

    /// <summary>The session is temporarily suspended.</summary>
    Paused,

    /// <summary>The session exited normally (exit code 0).</summary>
    Completed,

    /// <summary>The session exited with a non-zero exit code or an unhandled exception.</summary>
    Error,

    /// <summary>The session was forcefully terminated by the operator or watchdog.</summary>
    Terminated
}

/// <summary>In-memory session wrapper that couples an <see cref="ExecutionBlock"/> to its live resources.</summary>
public sealed record ShellSession(
    ExecutionBlock Block,
    PtyHandle? PtyHandle,
    Channel<OutputChunk> OutputChannel);

/// <summary>A single chunk of output from a PTY session.</summary>
/// <param name="SessionId">Session that produced this output.</param>
/// <param name="Stream">Which stream (<see cref="OutputStream.Stdout"/> or <see cref="OutputStream.Stderr"/>) emitted the chunk.</param>
/// <param name="Data">UTF-8 decoded content.</param>
/// <param name="Sequence">Monotonically increasing counter per session.</param>
/// <param name="Timestamp">UTC production timestamp.</param>
public sealed record OutputChunk(
    Guid SessionId,
    OutputStream Stream,
    string Data,
    long Sequence,
    DateTimeOffset Timestamp);

/// <summary>Identifies which output stream a chunk came from.</summary>
public enum OutputStream
{
    /// <summary>Standard output.</summary>
    Stdout,

    /// <summary>Standard error.</summary>
    Stderr
}

/// <summary>Tracks a single command execution within a session.</summary>
/// <param name="Id">Unique command execution identifier.</param>
/// <param name="SessionId">Parent session identifier.</param>
/// <param name="Command">The full command string that was executed.</param>
/// <param name="State">Current lifecycle state of the command.</param>
/// <param name="StartedAt">UTC timestamp when the command began executing.</param>
public sealed record CommandExecution(
    Guid Id,
    Guid SessionId,
    string Command,
    CommandState State,
    DateTimeOffset StartedAt);

/// <summary>Lifecycle state for a single command execution.</summary>
public enum CommandState
{
    /// <summary>Command accepted but not yet dispatched to the PTY.</summary>
    Pending,

    /// <summary>Command has been written to the PTY stdin.</summary>
    Running,

    /// <summary>Command exited cleanly.</summary>
    Completed,

    /// <summary>Command was cancelled by the caller.</summary>
    Cancelled,

    /// <summary>Command exited with an error or was killed.</summary>
    Failed
}

/// <summary>Handle to an active PTY-attached process.</summary>
/// <param name="ProcessId">OS process ID.</param>
/// <param name="ProcessName">Executable name as reported by the OS.</param>
/// <param name="Cols">Current terminal column count.</param>
/// <param name="Rows">Current terminal row count.</param>
public sealed record PtyHandle(
    int ProcessId,
    string ProcessName,
    int Cols,
    int Rows);

/// <summary>Options used when spawning a new PTY process.</summary>
/// <param name="Executable">Full path to the executable (e.g. <c>/bin/sh</c>).</param>
/// <param name="Arguments">Command-line arguments passed to the executable.</param>
/// <param name="WorkingDirectory">Working directory for the new process.</param>
/// <param name="Environment">Additional environment variables; merged with the host environment.</param>
/// <param name="Cols">Initial terminal column count.</param>
/// <param name="Rows">Initial terminal row count.</param>
public sealed record PtySpawnOptions(
    string Executable,
    string[] Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    int Cols = 220,
    int Rows = 50);

/// <summary>Request body for creating a new shell session.</summary>
/// <param name="Label">Human-readable label for the session.</param>
/// <param name="Shell">Shell executable (must be in <see cref="ShellVMConfig.AllowedShells"/>).</param>
/// <param name="WorkingDirectory">Initial working directory.</param>
/// <param name="Environment">Additional environment variables.</param>
/// <param name="RequestingModuleId">Module ID of the caller.</param>
public sealed record CreateSessionRequest(
    string Label,
    string Shell,
    string WorkingDirectory = "/app",
    IReadOnlyDictionary<string, string>? Environment = null,
    string? RequestingModuleId = null);

/// <summary>Request body for executing a one-shot command inside an existing session.</summary>
/// <param name="Command">Shell command string.</param>
/// <param name="WorkingDir">Override working directory for this command.</param>
/// <param name="Env">Additional environment variables for this command.</param>
/// <param name="TimeoutSeconds">Maximum execution time; 0 means use the module default.</param>
/// <param name="CaptureOutput">When <see langword="true"/>, output is streamed to subscribers.</param>
public sealed record ExecRequest(
    string Command,
    string? WorkingDir = null,
    IReadOnlyDictionary<string, string>? Env = null,
    int TimeoutSeconds = 0,
    bool CaptureOutput = true);

/// <summary>Request body for executing a named script with environment isolation.</summary>
/// <param name="ScriptPath">Absolute path to the script file.</param>
/// <param name="Arguments">Arguments to pass to the script interpreter.</param>
/// <param name="Interpreter">Interpreter override (e.g. <c>python3</c>); defaults to session shell.</param>
/// <param name="Env">Additional environment variables.</param>
/// <param name="TimeoutSeconds">Maximum execution time; 0 means use the module default.</param>
public sealed record ScriptRunRequest(
    string ScriptPath,
    string[] Arguments,
    string? Interpreter = null,
    IReadOnlyDictionary<string, string>? Env = null,
    int TimeoutSeconds = 0);

/// <summary>Query parameters for retrieving audit log entries.</summary>
/// <param name="SessionId">Restricts results to a single session.</param>
/// <param name="From">Inclusive lower bound on <c>started_at</c>.</param>
/// <param name="To">Inclusive upper bound on <c>started_at</c>.</param>
/// <param name="Limit">Maximum number of records to return.</param>
public sealed record AuditQuery(
    Guid? SessionId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100);

/// <summary>Resize request for a PTY session.</summary>
/// <param name="Cols">New column count.</param>
/// <param name="Rows">New row count.</param>
public sealed record ResizeRequest(int Cols, int Rows);
