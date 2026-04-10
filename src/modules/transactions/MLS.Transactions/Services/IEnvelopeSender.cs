using MLS.Core.Contracts;

namespace MLS.Transactions.Services;

/// <summary>Sends envelopes to the Block Controller SignalR hub.</summary>
public interface IEnvelopeSender
{
    Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default);
}
