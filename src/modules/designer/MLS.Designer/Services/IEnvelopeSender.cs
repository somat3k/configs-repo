using MLS.Core.Contracts;

namespace MLS.Designer.Services;

/// <summary>
/// Sends <see cref="EnvelopePayload"/> objects to the Block Controller REST API.
/// Used by controllers and services that need to dispatch strategy lifecycle events.
/// </summary>
public interface IEnvelopeSender
{
    /// <summary>
    /// POST an envelope to <c>POST /api/envelopes</c> on the Block Controller.
    /// Failures are logged and silently swallowed so callers are not blocked.
    /// </summary>
    Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default);
}
