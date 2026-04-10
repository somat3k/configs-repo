using Microsoft.AspNetCore.SignalR;
using MLS.Core.Contracts;

namespace MLS.Arbitrager.Hubs;

/// <summary>
/// SignalR hub for the Arbitrager module.
/// Connects on <c>/hubs/arbitrager</c>.
/// Clients join a group per their <c>moduleId</c> or <c>clientId</c> query parameter
/// and all connections join the <c>broadcast</c> group.
/// </summary>
public sealed class ArbitragerHub : Hub
{
    /// <summary>Hub method — client sends an envelope to the Arbitrager.</summary>
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
            var safeGroup = SanitisePeerId(peerId);
            await Groups.AddToGroupAsync(Context.ConnectionId, safeGroup).ConfigureAwait(false);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private static string SanitisePeerId(string id)
    {
        var truncated = id.Length > 64 ? id[..64] : id;
        return System.Text.RegularExpressions.Regex.Replace(truncated, @"[\r\n]", "_");
    }
}
