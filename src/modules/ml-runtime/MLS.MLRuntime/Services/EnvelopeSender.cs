using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Contracts;

namespace MLS.MLRuntime.Services;

/// <summary>
/// Concrete <see cref="IEnvelopeSender"/> that publishes envelopes to the Block Controller
/// SignalR hub using a typed <see cref="HttpClient"/> base address.
/// </summary>
public sealed class EnvelopeSender(
    HttpClient _http,
    ILogger<EnvelopeSender> _logger) : IEnvelopeSender
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;

    /// <inheritdoc/>
    public async Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default)
    {
        try
        {
            var hubConnection = await GetConnectedHubAsync(ct).ConfigureAwait(false);
            await hubConnection.InvokeAsync("SendEnvelope", envelope, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send envelope type={Type}", envelope.Type);
        }
    }

    private async Task<HubConnection> GetConnectedHubAsync(CancellationToken ct)
    {
        if (_hubConnection is { State: HubConnectionState.Connected })
        {
            return _hubConnection;
        }

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _hubConnection ??= CreateHubConnection();

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(ct).ConfigureAwait(false);
            }

            return _hubConnection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private HubConnection CreateHubConnection()
    {
        var baseAddress = _http.BaseAddress
            ?? throw new InvalidOperationException("EnvelopeSender requires HttpClient.BaseAddress to be configured.");

        var hubUri = new Uri(baseAddress, "/hubs/block-controller");

        return new HubConnectionBuilder()
            .WithUrl(hubUri)
            .WithAutomaticReconnect()
            .Build();
    }
}
