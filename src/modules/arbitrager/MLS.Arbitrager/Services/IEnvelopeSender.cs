using MLS.Core.Contracts;

namespace MLS.Arbitrager.Services;

/// <summary>
/// Posts <see cref="EnvelopePayload"/> messages to the Block Controller REST API.
/// </summary>
public interface IEnvelopeSender
{
    /// <summary>POST an envelope to <c>/api/envelopes</c> on the Block Controller.</summary>
    Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default);
}
