using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.ContainerRegistry.Hubs;

/// <summary>SignalR hub exposing the container-registry WebSocket API on port 6015.</summary>
public sealed class ContainerRegistryHub(
    IContainerRegistryService _service,
    ILogger<ContainerRegistryHub> _logger) : Hub<IContainerRegistryHubClient>
{
    /// <summary>Registers a container image and notifies the caller.</summary>
    public async Task RegisterImage(EnvelopePayload envelope)
    {
        var name     = envelope.Payload.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var tag      = envelope.Payload.TryGetProperty("tag", out var t) ? t.GetString() ?? "latest" : "latest";
        var registry = envelope.Payload.TryGetProperty("registry", out var r) ? r.GetString() ?? string.Empty : string.Empty;
        var digest   = envelope.Payload.TryGetProperty("digest", out var d) ? d.GetString() : null;

        var image    = await _service.RegisterImageAsync(new RegisterImageRequest(name, tag, registry, digest),
            Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.ContainerRegistered, ContainerRegistryConstants.ModuleName, image);
        await Clients.Caller.ReceiveImageStatus(response).ConfigureAwait(false);
    }

    /// <summary>Gets the status of a container image and sends it to the caller.</summary>
    public async Task GetImageStatus(EnvelopePayload envelope)
    {
        if (!envelope.Payload.TryGetProperty("image_id", out var idProp)
            || !Guid.TryParse(idProp.GetString(), out var imageId)) return;

        var image    = await _service.GetImageAsync(imageId, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.ContainerRegistered, ContainerRegistryConstants.ModuleName,
            new { image_id = imageId, image });
        await Clients.Caller.ReceiveImageStatus(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the ContainerRegistry hub.</summary>
public interface IContainerRegistryHubClient
{
    /// <summary>Receives a container image status update.</summary>
    Task ReceiveImageStatus(EnvelopePayload envelope);

    /// <summary>Receives a health check update.</summary>
    Task ReceiveHealthUpdate(EnvelopePayload envelope);
}
