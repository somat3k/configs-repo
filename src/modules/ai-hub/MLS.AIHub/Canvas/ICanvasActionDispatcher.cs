namespace MLS.AIHub.Canvas;

/// <summary>
/// Dispatches a <see cref="CanvasAction"/> to the web-app MDI canvas by sending an
/// <c>AI_CANVAS_ACTION</c> envelope via the AI Hub's own SignalR hub.
/// </summary>
/// <remarks>
/// Canvas-producing plugin functions MUST call
/// <see cref="DispatchAsync"/> BEFORE returning their string result so the
/// canvas panel opens in parallel with the streaming text response.
/// </remarks>
public interface ICanvasActionDispatcher
{
    /// <summary>
    /// Serialises <paramref name="action"/> into an <c>AI_CANVAS_ACTION</c> envelope
    /// and broadcasts it to the requesting user's SignalR group.
    /// </summary>
    /// <param name="action">The canvas action to dispatch.</param>
    /// <param name="userId">Target user identifier — routes to that user's SignalR group.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchAsync(CanvasAction action, Guid userId, CancellationToken ct = default);
}
