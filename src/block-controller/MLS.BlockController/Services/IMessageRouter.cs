using MLS.Core.Contracts;

namespace MLS.BlockController.Services;

/// <summary>
/// Routes and broadcasts <see cref="EnvelopePayload"/> messages between registered modules
/// using the subscription table.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Route an envelope to all modules subscribed to <c>envelope.Type</c>.
    /// Envelope is dropped silently if no subscribers exist (non-blocking hot path).
    /// </summary>
    Task RouteAsync(EnvelopePayload envelope, CancellationToken ct = default);

    /// <summary>Broadcast an envelope to ALL currently connected modules.</summary>
    Task BroadcastAsync(EnvelopePayload envelope, CancellationToken ct = default);
}
