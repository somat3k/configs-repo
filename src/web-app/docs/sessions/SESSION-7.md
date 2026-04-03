# web-app — Session 7: SignalR Hub (Web App Server-Side)

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 7. SignalR Hub (Web App Server-Side)


```csharp
namespace MLS.WebApp.Hubs;

/// <summary>
/// Web App's SignalR hub that proxies Block Controller events to browser clients.
/// Clients connect via JavaScript SignalR client in Blazor WASM.
/// </summary>
public sealed class DashboardHub(
    IBlockControllerHub _bc,
    ILogger<DashboardHub> _logger
) : Hub<IDashboardHubClient>
{
    public async Task SubscribeModules()    { /* join BC event stream */ }
    public async Task SubscribeTopic(string topic) { /* proxy BC envelope by type */ }
}

public interface IDashboardHubClient
{
    Task ReceiveModuleUpdate(ModuleStatusUpdate update);
    Task ReceiveEnvelope(EnvelopePayload envelope);
    Task ReceiveAlert(string message, AlertSeverity severity);
}
```

---
