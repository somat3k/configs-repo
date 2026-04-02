# shell-vm — Session 5: Payload Records (Envelope Payload Bodies)

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

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
