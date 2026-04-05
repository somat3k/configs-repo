using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.AIHub.Hubs;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using AIHubHub = MLS.AIHub.Hubs.AIHub;

namespace MLS.AIHub.Canvas;

/// <summary>
/// Dispatches <see cref="CanvasAction"/> instances to the web-app MDI canvas by sending
/// <c>AI_CANVAS_ACTION</c> envelopes to the requesting user's SignalR group via AI Hub's hub.
/// </summary>
public sealed class CanvasActionDispatcher(
    IHubContext<AIHubHub> _hubContext,
    ILogger<CanvasActionDispatcher> _logger) : ICanvasActionDispatcher
{
    private const string ModuleId = "ai-hub";

    /// <inheritdoc/>
    public async Task DispatchAsync(CanvasAction action, Guid userId, CancellationToken ct = default)
    {
        try
        {
            // Populate the typed AiCanvasActionPayload so consumers can deserialise
            // using the existing MLS.Core contract (action_type, panel_type, data, title).
            var (panelType, title) = action is OpenPanelAction open
                ? (open.PanelType, open.Title)
                : ((string?)null, (string?)null);

            var payload = new AiCanvasActionPayload(
                ActionType: action.ActionType,
                PanelType:  panelType,
                Data:       JsonSerializer.SerializeToElement(action),
                Title:      title);

            var envelope = EnvelopePayload.Create(
                MessageTypes.AiCanvasAction,
                ModuleId,
                payload);

            await _hubContext.Clients
                .Group(userId.ToString())
                .SendAsync("ReceiveEnvelope", envelope, ct)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "CanvasAction {ActionType} dispatched to user {UserId}",
                action.ActionType, userId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to dispatch CanvasAction {ActionType}", action.ActionType);
        }
    }
}
