using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.NetworkMask.Hubs;

/// <summary>SignalR hub exposing the network-mask WebSocket API on port 6016.</summary>
public sealed class NetworkMaskHub(
    INetworkMaskService _service,
    ILogger<NetworkMaskHub> _logger) : Hub<INetworkMaskHubClient>
{
    /// <summary>Resolves an endpoint and returns it to the caller.</summary>
    public async Task ResolveEndpoint(EnvelopePayload envelope)
    {
        var module = envelope.Payload.TryGetProperty("module_name", out var m) ? m.GetString() ?? string.Empty : string.Empty;
        var env    = envelope.Payload.TryGetProperty("environment", out var e) ? e.GetString() ?? "production" : "production";
        var info   = await _service.ResolveEndpointAsync(module, env, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.EndpointResolved, NetworkMaskConstants.ModuleName,
            new { module_name = module, environment = env, endpoint = info });
        await Clients.Caller.ReceiveEndpointInfo(response).ConfigureAwait(false);
    }

    /// <summary>Registers an endpoint.</summary>
    public async Task RegisterEndpoint(EnvelopePayload envelope)
    {
        var module = envelope.Payload.TryGetProperty("module_name", out var m) ? m.GetString() ?? string.Empty : string.Empty;
        var env    = envelope.Payload.TryGetProperty("environment", out var e) ? e.GetString() ?? "production" : "production";
        var http   = envelope.Payload.TryGetProperty("http_url", out var h) ? h.GetString() ?? string.Empty : string.Empty;
        var ws     = envelope.Payload.TryGetProperty("ws_url", out var w) ? w.GetString() ?? string.Empty : string.Empty;
        var tags   = Array.Empty<string>();

        var reg = new EndpointRegistration(module, env, http, ws, tags);
        await _service.RegisterEndpointAsync(reg, Context.ConnectionAborted).ConfigureAwait(false);

        var response = EnvelopePayload.Create(
            MessageTypes.EndpointRegistered, NetworkMaskConstants.ModuleName,
            new { module_name = module, environment = env });
        await Clients.Caller.ReceiveEndpointInfo(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the NetworkMask hub.</summary>
public interface INetworkMaskHubClient
{
    /// <summary>Receives endpoint information.</summary>
    Task ReceiveEndpointInfo(EnvelopePayload envelope);
}
