using Microsoft.AspNetCore.SignalR;
using MLS.Core.Contracts;
using MLS.WebApp.Services;

namespace MLS.WebApp.Hubs;

/// <summary>
/// Web App's server-side SignalR hub that proxies Block Controller events to
/// all connected browser clients (Blazor WASM / Interactive Server).
/// </summary>
public sealed class DashboardHub(
    IBlockControllerHub bc,
    ILogger<DashboardHub> logger) : Hub<IDashboardHubClient>
{
    /// <summary>Subscribe the calling connection to all module status updates.</summary>
    public async Task SubscribeModules(CancellationToken ct)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "modules", ct).ConfigureAwait(false);
        logger.LogDebug("Client {Id} subscribed to module updates", Context.ConnectionId);
    }

    /// <summary>Subscribe the calling connection to a specific envelope topic.</summary>
    public async Task SubscribeTopic(string topic, CancellationToken ct)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"topic:{topic}", ct).ConfigureAwait(false);
        logger.LogDebug("Client {Id} subscribed to topic {Topic}", Context.ConnectionId, topic);
    }

    /// <summary>Unsubscribe from an envelope topic.</summary>
    public async Task UnsubscribeTopic(string topic, CancellationToken ct)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"topic:{topic}", ct).ConfigureAwait(false);
    }
}

/// <summary>Typed hub client contract — methods called on the browser.</summary>
public interface IDashboardHubClient
{
    /// <summary>Delivers a module health/status update to the browser.</summary>
    Task ReceiveModuleUpdate(ModuleStatusUpdate update);

    /// <summary>Delivers a filtered envelope payload to the browser.</summary>
    Task ReceiveEnvelope(EnvelopePayload envelope);

    /// <summary>Delivers a platform-level alert message.</summary>
    Task ReceiveAlert(string message, string severity);
}
