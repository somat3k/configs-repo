# shell-vm — Session 9: Configuration

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

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
