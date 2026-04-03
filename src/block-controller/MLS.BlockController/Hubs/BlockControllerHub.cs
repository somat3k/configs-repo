using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using System.Text.Json;

namespace MLS.BlockController.Hubs;

/// <summary>
/// SignalR hub that is the single WebSocket entry point for all inter-module communication.
/// Modules connect on port 6100 and exchange <see cref="EnvelopePayload"/> messages.
/// </summary>
public sealed class BlockControllerHub(
    IStrategyRouter _strategyRouter,
    IMessageRouter _router,
    ILogger<BlockControllerHub> _logger) : Hub
{
    /// <summary>
    /// Primary entry point — called by connected modules to send an envelope.
    /// Routes by <see cref="EnvelopePayload.Type"/>.
    /// </summary>
    public async Task SendEnvelope(EnvelopePayload envelope)
    {
        _logger.LogDebug("Hub received {Type} from {ModuleId}", envelope.Type, envelope.ModuleId);

        await (envelope.Type switch
        {
            MessageTypes.StrategyDeploy   => HandleStrategyDeployAsync(envelope),
            MessageTypes.CanvasLayoutSave => HandleCanvasLayoutSaveAsync(envelope),
            _                             => _router.RouteAsync(envelope)
        }).ConfigureAwait(false);
    }

    // ── Private handlers ─────────────────────────────────────────────────────────

    private async Task HandleStrategyDeployAsync(EnvelopePayload envelope)
    {
        StrategyGraphPayload graph;
        try
        {
            graph = envelope.Payload.Deserialize<StrategyGraphPayload>()
                ?? throw new InvalidOperationException("Null STRATEGY_DEPLOY payload.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialise STRATEGY_DEPLOY payload");
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
                ?? throw new InvalidOperationException("Null CANVAS_LAYOUT_SAVE payload.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialise CANVAS_LAYOUT_SAVE payload");
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
}
