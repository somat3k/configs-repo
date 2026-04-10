using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.UniqueIdGenerator.Hubs;

/// <summary>SignalR hub exposing the unique-id-generator WebSocket API on port 6010.</summary>
public sealed class UniqueIdGeneratorHub(
    IUniqueIdService _service,
    ILogger<UniqueIdGeneratorHub> _logger) : Hub<IUniqueIdGeneratorHubClient>
{
    /// <summary>Generates a UUID and returns it to the caller via <c>ReceiveId</c>.</summary>
    public async Task GenerateUuid(EnvelopePayload envelope)
    {
        var id = _service.GenerateUuid();
        var response = EnvelopePayload.Create(
            MessageTypes.IdGenerated,
            UniqueIdGeneratorConstants.ModuleName,
            new { id, type = "uuid" });
        await Clients.Caller.ReceiveId(response).ConfigureAwait(false);
    }

    /// <summary>Generates a sequential ID and returns it to the caller via <c>ReceiveId</c>.</summary>
    public async Task GenerateSequentialId(EnvelopePayload envelope)
    {
        var prefix = envelope.Payload.TryGetProperty("prefix", out var p)
            ? p.GetString() ?? "default"
            : "default";
        var id = _service.GenerateSequentialId(prefix);
        var response = EnvelopePayload.Create(
            MessageTypes.IdGenerated,
            UniqueIdGeneratorConstants.ModuleName,
            new { id, prefix, type = "sequential" });
        await Clients.Caller.ReceiveId(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the UniqueIdGenerator hub.</summary>
public interface IUniqueIdGeneratorHubClient
{
    /// <summary>Receives a generated ID result.</summary>
    Task ReceiveId(EnvelopePayload envelope);
}
