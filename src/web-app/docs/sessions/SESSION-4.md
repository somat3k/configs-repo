# web-app — Session 4: Real-Time Data Architecture

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 4. Real-Time Data Architecture


```csharp
namespace MLS.WebApp.Services;

/// <summary>Manages SignalR connection to Block Controller for live module events.</summary>
public interface IBlockControllerHub
{
    Task ConnectAsync(CancellationToken ct);
    IAsyncEnumerable<ModuleStatusUpdate> GetModuleUpdatesAsync(CancellationToken ct);
    IAsyncEnumerable<EnvelopePayload> GetEnvelopeStreamAsync(string[] topics, CancellationToken ct);
}

/// <summary>Subscribes to Shell VM WebSocket for real-time terminal output.</summary>
public interface IShellVMClient
{
    Task<Guid> StartSessionAsync(string command, CancellationToken ct);
    IAsyncEnumerable<ShellOutputChunk> GetOutputStreamAsync(Guid sessionId, CancellationToken ct);
    ValueTask SendInputAsync(Guid sessionId, string input, CancellationToken ct);
    Task TerminateSessionAsync(Guid sessionId, CancellationToken ct);
}
```

---
