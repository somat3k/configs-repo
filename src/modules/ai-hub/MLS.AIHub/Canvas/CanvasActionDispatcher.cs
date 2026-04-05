using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.AIHub.Hubs;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.AIHub.Canvas;

/// <summary>
/// Dispatches <see cref="CanvasAction"/> instances to the web-app MDI canvas by sending
/// <c>AI_CANVAS_ACTION</c> envelopes to the requesting user's SignalR group.
/// </summary>
public sealed class CanvasActionDispatcher(
    IHubContext<AIHubSignalR> _hubContext,
    ILogger<CanvasActionDispatcher> _logger) : ICanvasActionDispatcher
{
    private const string ModuleId = "ai-hub";

    /// <inheritdoc/>
    public async Task DispatchAsync(CanvasAction action, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                action_type = action.ActionType,
                data        = JsonSerializer.SerializeToElement(action),
            };

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
