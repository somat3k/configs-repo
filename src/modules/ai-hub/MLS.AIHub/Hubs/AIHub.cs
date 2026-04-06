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

        // Resolve userId from the connection's clientId query param.
        // NOTE: clientId is trusted for group routing; production deployments should enforce
        // authentication and derive this from a verified principal instead.
        var httpCtx  = Context.GetHttpContext();
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

        // Validate required fields
        if (string.IsNullOrWhiteSpace(queryPayload.Query))
        {
            _logger.LogWarning(
                "AIHub: AI_QUERY has empty Query for envelope {SessionId} — dropping",
                envelope.SessionId);
            return Task.CompletedTask;
        }

        // Normalize optional collection — JSON may omit the field entirely
        if (queryPayload.ConversationHistory is null)
            queryPayload = queryPayload with { ConversationHistory = [] };

        // Capture connection-abort token before the fire-and-forget so the background
        // task can cancel AI work when the caller's connection drops.
        var connectionAborted = Context.ConnectionAborted;

        _ = ProcessInScopeAsync(queryPayload, userId, connectionAborted);
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ProcessInScopeAsync(
        AiQueryPayload query, Guid userId, CancellationToken connectionAborted)
    {
        using var scope      = _scopeFactory.CreateScope();
        var chatService      = scope.ServiceProvider.GetRequiredService<IChatService>();

        // Link connection-abort with a 2-minute hard timeout to prevent provider spend
        // from a long-running request with no active listener.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionAborted);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            await chatService.ProcessQueryAsync(query, userId, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (connectionAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "AIHub: AI_QUERY processing cancelled — connection aborted for user {UserId}", userId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "AIHub: AI_QUERY processing timed out (2 min) for user {UserId}", userId);
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

