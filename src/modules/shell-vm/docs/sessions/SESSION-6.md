# shell-vm — Session 6: SignalR Hub

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 6. SignalR Hub


```csharp
namespace MLS.ShellVM.Hubs;

/// <summary>
/// SignalR hub exposing shell-vm WebSocket API on port 6950.
/// Handles command execution requests, PTY input, resize events,
/// and real-time output streaming.
/// </summary>
public sealed class ShellVMHub(
    ISessionManager _sessions,
    IExecutionEngine _engine,
    IOutputBroadcaster _broadcaster,
    ILogger<ShellVMHub> _logger
) : Hub<IShellVMHubClient>
{
    public async Task ExecCommand(EnvelopePayload envelope) { /* dispatch to engine */ }
    public async Task SendInput(EnvelopePayload envelope)   { /* forward bytes to PTY */ }
    public async Task ResizePty(EnvelopePayload envelope)   { /* update cols×rows */ }
    public async Task SubscribeSession(Guid sessionId)      { /* join SignalR group */ }
    public async Task UnsubscribeSession(Guid sessionId)    { /* leave SignalR group */ }
    public override async Task OnDisconnectedAsync(Exception? ex)  { /* auto-unsubscribe */ }
}

/// <summary>Client-side methods pushed by the hub.</summary>
public interface IShellVMHubClient
{
    Task ReceiveOutput(EnvelopePayload envelope);
    Task ReceiveSessionState(EnvelopePayload envelope);
    Task ReceiveSessionCreated(EnvelopePayload envelope);
    Task ReceiveSessionTerminated(EnvelopePayload envelope);
    Task ReceiveHeartbeat(EnvelopePayload envelope);
}
```

---
