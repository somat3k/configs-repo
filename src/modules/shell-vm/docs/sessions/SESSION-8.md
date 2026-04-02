# shell-vm — Session 8: Background Services

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

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
