using MLS.Core.Contracts;

namespace MLS.MLRuntime.Services;

/// <summary>Sends <see cref="EnvelopePayload"/> messages to Block Controller.</summary>
public interface IEnvelopeSender
{
    /// <summary>POSTs an envelope to the Block Controller REST API.</summary>
    Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default);
}
