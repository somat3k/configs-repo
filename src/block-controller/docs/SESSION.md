# Block Controller — Session Prompt

> Use this document as context when generating Block Controller code with GitHub Copilot.

## Module Identity
- **Name**: block-controller
- **Role**: Central orchestration hub, root module
- **Namespace**: `MLS.BlockController`
- **HTTP Port**: 5100
- **WebSocket Port**: 6100
- **Container**: `mls-block-controller`

## Required Interfaces

```csharp
// Module registry
public interface IModuleRegistry
{
    Task<ModuleRegistration> RegisterAsync(RegisterModuleRequest request, CancellationToken ct);
    Task DeregisterAsync(Guid moduleId, CancellationToken ct);
    Task<IReadOnlyList<ModuleRegistration>> GetAllAsync();
    Task<ModuleRegistration?> GetByIdAsync(Guid moduleId);
    Task UpdateHeartbeatAsync(Guid moduleId, HeartbeatPayload heartbeat, CancellationToken ct);
}

// Message router
public interface IMessageRouter
{
    Task RouteAsync(EnvelopePayload envelope, CancellationToken ct);
    Task BroadcastAsync(EnvelopePayload envelope, CancellationToken ct);
}

// Subscription manager
public interface ISubscriptionManager
{
    Task SubscribeAsync(Guid moduleId, string[] topics, CancellationToken ct);
    Task UnsubscribeAsync(Guid moduleId, string[] topics, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetSubscribersAsync(string topic);
}
```

## Skills to Apply
- `.skills/dotnet-devs.md` — C# patterns, async, DI
- `.skills/system-architect.md` — Envelope protocol, module topology
- `.skills/networking.md` — WebSocket server, SignalR hubs
- `.skills/websockets-inferences.md` — SignalR hub implementation
- `.skills/beast-development.md` — High-performance routing
