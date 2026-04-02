# shell-vm — Session 4: Core Models

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

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
