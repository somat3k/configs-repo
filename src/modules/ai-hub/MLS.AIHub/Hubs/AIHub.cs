using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.AIHub.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using System.Text.Json;

namespace MLS.AIHub.Hubs;

/// <summary>
/// SignalR hub for the AI Hub module.
/// Accepts <c>AI_QUERY</c> envelopes from connected clients and pushes
/// <c>AI_RESPONSE_CHUNK</c>, <c>AI_CANVAS_ACTION</c>, and <c>AI_RESPONSE_COMPLETE</c>
/// envelopes back to the requester via their personal group.
/// </summary>
/// <remarks>
/// Clients connect with <c>?clientId=&lt;guid&gt;</c> for bidirectional access.
/// Module connections use <c>?moduleId=&lt;guid&gt;</c>.
/// All connections join the <c>broadcast</c> group automatically.
/// </remarks>
public sealed class AIHub(
    IServiceScopeFactory _scopeFactory,
    ILogger<AIHub> _logger) : Hub
{
    /// <summary>Group name for platform-wide broadcast envelopes.</summary>
    public const string BroadcastGroup = "broadcast";

    // ── Connection lifecycle ──────────────────────────────────────────────────

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

    // ── Hub methods ───────────────────────────────────────────────────────────

    /// <summary>
    /// Primary entry point — receives an envelope from any connected client.
    /// <c>AI_QUERY</c> envelopes trigger the full chat pipeline; others are acknowledged and ignored.
    /// </summary>
    public Task SendEnvelope(EnvelopePayload envelope)
    {
        if (envelope.Type != MessageTypes.AiQuery)
        {
            _logger.LogDebug("AIHub: ignoring non-AI_QUERY envelope {Type}", envelope.Type);
            return Task.CompletedTask;
        }

        // Resolve userId from the connection's clientId query param
        var httpCtx = Context.GetHttpContext();
        var clientId = httpCtx?.Request.Query["clientId"].FirstOrDefault();

        if (!Guid.TryParse(clientId, out var userId))
        {
            _logger.LogWarning(
                "AIHub: AI_QUERY received from connection without a valid clientId — dropping");
            return Task.CompletedTask;
        }

        // Deserialise the query payload
        AiQueryPayload? queryPayload;
        try
        {
            queryPayload = JsonSerializer.Deserialize<AiQueryPayload>(envelope.Payload.GetRawText());
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "AIHub: failed to deserialise AiQueryPayload for envelope {SessionId} — dropping",
                envelope.SessionId);
            return Task.CompletedTask;
        }

        if (queryPayload is null)
        {
            _logger.LogWarning(
                "AIHub: AiQueryPayload deserialised to null for envelope {SessionId} — dropping",
                envelope.SessionId);
            return Task.CompletedTask;
        }

        // Process on a background scope — ChatService is scoped and must not outlive the call
        _ = ProcessInScopeAsync(queryPayload, userId);
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ProcessInScopeAsync(AiQueryPayload query, Guid userId)
    {
        using var scope   = _scopeFactory.CreateScope();
        var chatService   = scope.ServiceProvider.GetRequiredService<IChatService>();
        try
        {
            await chatService.ProcessQueryAsync(query, userId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AIHub: unhandled error processing AI_QUERY for user {UserId}", userId);
        }
    }

    private static string SanitizeId(string id) =>
        id.Length > 64
            ? id[..64].Replace('\r', '_').Replace('\n', '_')
            : id.Replace('\r', '_').Replace('\n', '_');
}
