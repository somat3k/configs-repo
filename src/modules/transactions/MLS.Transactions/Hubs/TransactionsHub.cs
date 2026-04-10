using Microsoft.AspNetCore.SignalR;
using MLS.Core.Contracts;

namespace MLS.Transactions.Hubs;

/// <summary>
/// SignalR hub for the Transactions module. Accepts incoming envelopes and
/// broadcasts to topic subscribers.
/// </summary>
public sealed class TransactionsHub : Hub
{
    /// <summary>Receives an envelope from a client and broadcasts it to the broadcast group.</summary>
    public async Task SendEnvelope(EnvelopePayload envelope)
    {
        await Clients.Group("broadcast").SendAsync("ReceiveEnvelope", envelope).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}
