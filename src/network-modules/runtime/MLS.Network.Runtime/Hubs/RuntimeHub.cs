using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.Runtime.Hubs;

/// <summary>SignalR hub exposing the runtime WebSocket API on port 6013.</summary>
public sealed class RuntimeHub(
    IModuleRuntimeService _service,
    ILogger<RuntimeHub> _logger) : Hub<IRuntimeHubClient>
{
    /// <summary>Retrieves module status and sends it to the caller.</summary>
    public async Task GetModuleStatus(EnvelopePayload envelope)
    {
        var name = envelope.Payload.TryGetProperty("module_name", out var m)
            ? m.GetString() ?? string.Empty : string.Empty;
        var status = await _service.GetStatusAsync(name, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.ModuleStatusUpdate,
            RuntimeConstants.ModuleName,
            status);
        await Clients.Caller.ReceiveModuleStatus(response).ConfigureAwait(false);
    }

    /// <summary>Starts a module container.</summary>
    public async Task StartModule(EnvelopePayload envelope)
    {
        var name = envelope.Payload.TryGetProperty("module_name", out var m)
            ? m.GetString() ?? string.Empty : string.Empty;
        await _service.StartModuleAsync(name, Context.ConnectionAborted).ConfigureAwait(false);
        var status = await _service.GetStatusAsync(name, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(MessageTypes.ModuleStarted, RuntimeConstants.ModuleName, status);
        await Clients.Caller.ReceiveModuleStatus(response).ConfigureAwait(false);
    }

    /// <summary>Stops a module container.</summary>
    public async Task StopModule(EnvelopePayload envelope)
    {
        var name = envelope.Payload.TryGetProperty("module_name", out var m)
            ? m.GetString() ?? string.Empty : string.Empty;
        await _service.StopModuleAsync(name, Context.ConnectionAborted).ConfigureAwait(false);
        var status = await _service.GetStatusAsync(name, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(MessageTypes.ModuleStopped, RuntimeConstants.ModuleName, status);
        await Clients.Caller.ReceiveModuleStatus(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the Runtime hub.</summary>
public interface IRuntimeHubClient
{
    /// <summary>Receives a module status update.</summary>
    Task ReceiveModuleStatus(EnvelopePayload envelope);

    /// <summary>Receives a container log line.</summary>
    Task ReceiveModuleLog(EnvelopePayload envelope);
}
