using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Contracts;

namespace MLS.Trader.Services;

/// <summary>
/// Concrete <see cref="IEnvelopeSender"/> that dispatches envelopes to the Block Controller
/// SignalR hub using the documented <c>SendEnvelope</c> hub method.
/// </summary>
public sealed class EnvelopeSender(
    HttpClient _http,
    ILogger<EnvelopeSender> _logger) : IEnvelopeSender, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;

    /// <inheritdoc/>
    public async Task SendEnvelopeAsync(EnvelopePayload envelope, CancellationToken ct = default)
    {
        try
        {
            var connection = await GetConnectedHubAsync(ct).ConfigureAwait(false);
            await connection.InvokeAsync("SendEnvelope", envelope, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send envelope type={Type}", envelope.Type);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync().ConfigureAwait(false);
        _connectionLock.Dispose();
    }

    private async Task<HubConnection> GetConnectedHubAsync(CancellationToken ct)
    {
        if (_hubConnection is { State: HubConnectionState.Connected })
            return _hubConnection;

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _hubConnection ??= CreateHubConnection();

            if (_hubConnection.State == HubConnectionState.Disconnected)
                await _hubConnection.StartAsync(ct).ConfigureAwait(false);

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
            ?? throw new InvalidOperationException(
                "EnvelopeSender requires HttpClient.BaseAddress to be configured pointing to the Block Controller endpoint (hub path: /hubs/block-controller).");

        var hubUri = new Uri(baseAddress, "/hubs/block-controller");

        return new HubConnectionBuilder()
            .WithUrl(hubUri)
            .WithAutomaticReconnect()
            .Build();
    }
}