using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;

namespace MLS.BlockController.Hubs;

/// <summary>
/// SignalR hub that is the single WebSocket entry point for all inter-module communication
/// and for external users/tools connecting to the MLS platform.
/// </summary>
/// <remarks>
/// <b>Bidirectional value-exchange protocol:</b> every connected client (module or external
/// user) is treated symmetrically — it can both SEND envelopes (client output → hub input)
/// and RECEIVE envelopes (hub output → client input). This makes the Block Controller a
/// true network node:
/// <list type="bullet">
///   <item>Modules connect with <c>?moduleId=&lt;guid&gt;</c> and join their own group.</item>
///   <item>External clients (tools, dashboards) connect with <c>?clientId=&lt;guid&gt;</c>.</item>
///   <item>All connected clients join the <c>broadcast</c> group automatically.</item>
///   <item>Clients can subscribe/unsubscribe to any topic group dynamically.</item>
/// </list>
/// The subscription table drives targeted delivery; the broadcast group handles platform-wide
/// events (strategy state changes, health warnings, etc.).
/// </remarks>
public sealed class BlockControllerHub(
    IStrategyRouter _strategyRouter,
    IMessageRouter _router,
    ISubscriptionTable _subscriptions,
    ILogger<BlockControllerHub> _logger) : Hub
{
    /// <summary>Group name that receives all platform-wide broadcast envelopes.</summary>
    public const string BroadcastGroup = "broadcast";

    // ── Connection lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Called when a client connects. Adds the connection to the broadcast group and,
    /// if a module/client ID is provided via query string, to that ID's own group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()?.Request.Query;
        var peerId = query?["moduleId"].FirstOrDefault()
                  ?? query?["clientId"].FirstOrDefault();

        // Every connection receives platform broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, BroadcastGroup).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(peerId))
        {
            // Targeted delivery: envelopes routed to this peer ID arrive here
            await Groups.AddToGroupAsync(Context.ConnectionId, peerId).ConfigureAwait(false);
            // Sanitize user-supplied value before logging to prevent log forging
            _logger.LogInformation("Peer {PeerId} connected (connection {ConnectionId})",
                SanitizeId(peerId), Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Anonymous client connected (connection {ConnectionId})",
                Context.ConnectionId);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Called when a client disconnects. Removes the connection from all groups.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var query = Context.GetHttpContext()?.Request.Query;
        var peerId = query?["moduleId"].FirstOrDefault()
                  ?? query?["clientId"].FirstOrDefault();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BroadcastGroup).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(peerId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, peerId).ConfigureAwait(false);
            // Sanitize user-supplied value before logging to prevent log forging
            _logger.LogInformation("Peer {PeerId} disconnected (connection {ConnectionId})",
                SanitizeId(peerId), Context.ConnectionId);
        }

        if (exception is not null)
        {
            _logger.LogWarning(exception, "Connection {ConnectionId} closed with error",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    // ── Envelope entry point ─────────────────────────────────────────────────────

    /// <summary>
    /// Primary entry point — called by connected peers to send an envelope.
    /// Routes by <see cref="EnvelopePayload.Type"/>.
    /// </summary>
    public async Task SendEnvelope(EnvelopePayload envelope)
    {
        _logger.LogDebug("Hub received {Type} from {ModuleId}", envelope.Type, envelope.ModuleId);

        await (envelope.Type switch
        {
            MessageTypes.StrategyDeploy => HandleStrategyDeployAsync(envelope),
            MessageTypes.CanvasLayoutSave => HandleCanvasLayoutSaveAsync(envelope),
            _ => _router.RouteAsync(envelope)
        }).ConfigureAwait(false);
    }

    // ── Dynamic topic subscription ───────────────────────────────────────────────

    /// <summary>
    /// Subscribe the calling connection to a named topic group so it receives
    /// envelopes routed to that topic.
    /// </summary>
    /// <param name="topic">Topic name matching <see cref="ISubscriptionTable"/> keys.</param>
    public async Task SubscribeToTopicAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var query = Context.GetHttpContext()?.Request.Query;
        var peerId = query?["moduleId"].FirstOrDefault()
                  ?? query?["clientId"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(peerId) && Guid.TryParse(peerId, out var subscriberId))
        {
            await _subscriptions.AddAsync(topic, subscriberId).ConfigureAwait(false);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, topic).ConfigureAwait(false);
        _logger.LogDebug("Connection {ConnectionId} subscribed to topic {Topic}",
            Context.ConnectionId, topic);
    }

    /// <summary>
    /// Unsubscribe the calling connection from a named topic group.
    /// </summary>
    /// <param name="topic">Topic to unsubscribe from.</param>
    public async Task UnsubscribeFromTopicAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var query = Context.GetHttpContext()?.Request.Query;
        var peerId = query?["moduleId"].FirstOrDefault()
                  ?? query?["clientId"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(peerId) && Guid.TryParse(peerId, out var subscriberId))
        {
            await _subscriptions.RemoveAsync(topic, subscriberId).ConfigureAwait(false);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, topic).ConfigureAwait(false);
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from topic {Topic}",
            Context.ConnectionId, topic);
    }

    // ── Private handlers ─────────────────────────────────────────────────────────

    private async Task HandleStrategyDeployAsync(EnvelopePayload envelope)
    {
        StrategyGraphPayload graph;
        try
        {
            graph = envelope.Payload.Deserialize<StrategyGraphPayload>()
                ?? throw new InvalidOperationException(
                    "STRATEGY_DEPLOY payload deserialized to null — the JSON object may be empty or missing required fields.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to process STRATEGY_DEPLOY from {ModuleId}",
                envelope.ModuleId);
            return;
        }

        await _strategyRouter.DeployAsync(graph).ConfigureAwait(false);
    }

    private async Task HandleCanvasLayoutSaveAsync(EnvelopePayload envelope)
    {
        CanvasLayoutSavePayload layout;
        try
        {
            layout = envelope.Payload.Deserialize<CanvasLayoutSavePayload>()
                ?? throw new InvalidOperationException(
                    "CANVAS_LAYOUT_SAVE payload deserialized to null — the JSON object may be empty or missing required fields.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to process CANVAS_LAYOUT_SAVE from {ModuleId}",
                envelope.ModuleId);
            return;
        }

        _logger.LogInformation("Canvas layout saved for user {UserId} ({WindowCount} windows)",
            layout.UserId, layout.Layout.Count);

        // Broadcast the layout to all connected clients (e.g. for multi-tab sync)
        await _router.BroadcastAsync(
            EnvelopePayload.Create(
                MessageTypes.CanvasLayoutSave,
                "block-controller",
                layout)).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes newline/carriage-return characters from a user-supplied ID before it is
    /// written to a log entry, preventing log-forging attacks (CWE-117).
    /// </summary>
    private static string SanitizeId(string id) =>
        id.Length > 64
            ? id[..64].Replace('\r', '_').Replace('\n', '_')
            : id.Replace('\r', '_').Replace('\n', '_');
}
