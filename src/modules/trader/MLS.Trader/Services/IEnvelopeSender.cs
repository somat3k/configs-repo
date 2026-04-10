using MLS.Core.Contracts;

namespace MLS.Trader.Services;

/// <summary>
/// Sends <see cref="EnvelopePayload"/> instances to the Block Controller REST API for routing.
/// </summary>
public interface IEnvelopeSender
{
    /// <summary>
    /// POSTs <paramref name="envelope"/> to the Block Controller <c>/api/envelopes</c> endpoint.
    /// Failures are logged and swallowed so that the caller's hot path is never interrupted.
    /// </summary>
    Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default);
}
