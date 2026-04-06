using Microsoft.AspNetCore.SignalR;
using MLS.Core.Contracts;

namespace MLS.DataLayer.Hubs;

/// <summary>
/// SignalR hub for the Data Layer module.
/// Connects on <c>/hubs/data-layer</c>.
/// Clients join a group per their <c>moduleId</c> or <c>clientId</c> query parameter
/// and all connections join the <c>broadcast</c> group.
/// </summary>
public sealed class DataLayerHub : Hub
{
    /// <summary>Hub method — client sends an envelope to the Data Layer.</summary>
    public Task SendEnvelope(EnvelopePayload envelope) =>
        Clients.Group("broadcast").SendAsync("ReceiveEnvelope", envelope);

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var http     = Context.GetHttpContext();
        var moduleId = http?.Request.Query["moduleId"].ToString();
        var clientId = http?.Request.Query["clientId"].ToString();

        var peerId = !string.IsNullOrWhiteSpace(moduleId) ? moduleId
                   : !string.IsNullOrWhiteSpace(clientId) ? clientId
                   : null;

        if (peerId is not null)
        {
            var safeGroup = peerId.Replace(" ", "_");
            await Groups.AddToGroupAsync(Context.ConnectionId, safeGroup).ConfigureAwait(false);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}
