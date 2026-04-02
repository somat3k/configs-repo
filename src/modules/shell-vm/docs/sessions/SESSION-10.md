# shell-vm — Session 10: Constants

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

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
