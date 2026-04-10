using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.VirtualMachine.Hubs;

/// <summary>SignalR hub exposing the virtual-machine WebSocket API on port 6014.</summary>
public sealed class VirtualMachineHub(
    IVirtualMachineService _service,
    ILogger<VirtualMachineHub> _logger) : Hub<IVirtualMachineHubClient>
{
    /// <summary>Executes a strategy script and returns the result.</summary>
    public async Task ExecuteStrategy(EnvelopePayload envelope)
    {
        var script = envelope.Payload.TryGetProperty("script", out var s)
            ? s.GetString() ?? string.Empty : string.Empty;
        var request = new SandboxRequest(script);
        var result  = await _service.ExecuteAsync(request, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.SandboxResult,
            VirtualMachineConstants.ModuleName,
            result);
        await Clients.Caller.ReceiveSandboxResult(response).ConfigureAwait(false);
    }

    /// <summary>Gets sandbox status by ID.</summary>
    public async Task GetSandboxStatus(EnvelopePayload envelope)
    {
        if (!envelope.Payload.TryGetProperty("sandbox_id", out var idProp)
            || !Guid.TryParse(idProp.GetString(), out var sandboxId))
        {
            return;
        }
        var info = await _service.GetSandboxAsync(sandboxId, Context.ConnectionAborted).ConfigureAwait(false);
        var response = EnvelopePayload.Create(
            MessageTypes.SandboxExecuted,
            VirtualMachineConstants.ModuleName,
            new { sandbox_id = sandboxId, info });
        await Clients.Caller.ReceiveSandboxStatus(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the VirtualMachine hub.</summary>
public interface IVirtualMachineHubClient
{
    /// <summary>Receives a sandbox execution result.</summary>
    Task ReceiveSandboxResult(EnvelopePayload envelope);

    /// <summary>Receives a sandbox status update.</summary>
    Task ReceiveSandboxStatus(EnvelopePayload envelope);
}
