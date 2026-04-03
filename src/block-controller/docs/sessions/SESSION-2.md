# block-controller — Session 2: Required Interfaces

> Use this document as context when generating Block Controller code with GitHub Copilot.

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
