using System.Net.Http.Json;
using MLS.Core.Contracts;

namespace MLS.DataLayer.Services;

/// <summary>
/// Concrete <see cref="IEnvelopeSender"/> that POSTs envelope payloads
/// to the Block Controller REST API using a named <see cref="HttpClient"/>.
/// </summary>
public sealed class EnvelopeSender(
    HttpClient _http,
    ILogger<EnvelopeSender> _logger) : IEnvelopeSender
{
    /// <inheritdoc/>
    public async Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("/api/envelopes", envelope, ct)
                                             .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("SendEnvelope returned {Status} for type={Type}",
                    response.StatusCode, envelope.Type);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send envelope type={Type}", envelope.Type);
        }
    }
}
