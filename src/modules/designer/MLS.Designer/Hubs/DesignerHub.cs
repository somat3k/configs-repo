using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.Core.Contracts;
using MLS.Designer.Services;

namespace MLS.Designer.Hubs;

/// <summary>
/// SignalR hub for the Designer module.
/// Accepts <c>STRATEGY_DEPLOY</c>, <c>BLOCK_SIGNAL</c> and other Designer-domain envelopes
/// from connected clients and exposes a push channel for canvas updates.
/// </summary>
/// <remarks>
/// Clients connect with <c>?clientId=&lt;guid&gt;</c> for bidirectional access.
/// Module clients connect with <c>?moduleId=&lt;guid&gt;</c>.
/// All connections join the <c>broadcast</c> group automatically.
/// </remarks>
public sealed class DesignerHub(
    IBlockRegistry _registry,
    ILogger<DesignerHub> _logger) : Hub
{
    /// <summary>Group name for platform-wide broadcast envelopes.</summary>
    public const string BroadcastGroup = "broadcast";

    // ── Connection lifecycle ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var query  = Context.GetHttpContext()?.Request.Query;
        var peerId = query?["moduleId"].FirstOrDefault() ?? query?["clientId"].FirstOrDefault();

        await Groups.AddToGroupAsync(Context.ConnectionId, BroadcastGroup).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(peerId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SanitizeId(peerId)).ConfigureAwait(false);
            _logger.LogInformation("Designer: peer {PeerId} connected", SanitizeId(peerId));
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var query  = Context.GetHttpContext()?.Request.Query;
        var peerId = query?["moduleId"].FirstOrDefault() ?? query?["clientId"].FirstOrDefault();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BroadcastGroup).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(peerId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SanitizeId(peerId)).ConfigureAwait(false);

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    // ── Hub methods ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Primary entry point — receives an envelope from any connected client.
    /// Routes to the appropriate internal handler or broadcasts.
    /// </summary>
    public Task SendEnvelope(EnvelopePayload envelope)
    {
        _logger.LogDebug("DesignerHub received {Type} from {ModuleId}", envelope.Type, envelope.ModuleId);
        // Additional envelope routing to be added in later sessions
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the list of all registered block metadata as a push message to the caller.
    /// </summary>
    public Task GetBlockCatalogAsync() =>
        Clients.Caller.SendAsync("BlockCatalog", _registry.GetAll());

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string SanitizeId(string id) =>
        id.Length > 64
            ? id[..64].Replace('\r', '_').Replace('\n', '_')
            : id.Replace('\r', '_').Replace('\n', '_');
}
