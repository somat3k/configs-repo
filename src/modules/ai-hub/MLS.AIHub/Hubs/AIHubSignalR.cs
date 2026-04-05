using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.Core.Contracts;
using MLS.Core.Constants;

namespace MLS.AIHub.Hubs;

/// <summary>
/// SignalR hub for the AI Hub module.
/// Accepts <c>AI_QUERY</c> envelopes from connected clients and pushes
/// <c>AI_RESPONSE_CHUNK</c>, <c>AI_CANVAS_ACTION</c>, and <c>AI_RESPONSE_COMPLETE</c>
/// envelopes back to the requester.
/// </summary>
/// <remarks>
/// Clients connect with <c>?clientId=&lt;guid&gt;</c> for bidirectional access.
/// Module clients connect with <c>?moduleId=&lt;guid&gt;</c>.
/// All connections join the <c>broadcast</c> group automatically.
/// </remarks>
public sealed class AIHubSignalR(
    ILogger<AIHubSignalR> _logger) : Hub
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
            _logger.LogInformation("AIHub: peer {PeerId} connected", SanitizeId(peerId));
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
    /// Handles <c>AI_QUERY</c> envelopes; others are acknowledged and ignored.
    /// </summary>
    public Task SendEnvelope(EnvelopePayload envelope)
    {
        _logger.LogDebug("AIHubSignalR received {Type} from {ModuleId}", envelope.Type, envelope.ModuleId);
        // Chat processing (AI_QUERY → SK invoke → AI_RESPONSE_CHUNK stream) implemented in Session 09.
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string SanitizeId(string id) =>
        id.Length > 64
            ? id[..64].Replace('\r', '_').Replace('\n', '_')
            : id.Replace('\r', '_').Replace('\n', '_');
}
