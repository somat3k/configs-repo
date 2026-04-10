using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Contracts;
using MLS.Transactions.Configuration;

namespace MLS.Transactions.Services;

/// <summary>
/// Dispatches envelopes to the Block Controller SignalR hub via <c>SendEnvelope</c>.
/// </summary>
public sealed class EnvelopeSender(
    HttpClient _http,
    IOptions<TransactionsOptions> _options,
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
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_hubConnection is { State: HubConnectionState.Connected })
                return _hubConnection;

            if (_hubConnection is not null)
                await _hubConnection.DisposeAsync().ConfigureAwait(false);

            var bcUrl = _options.Value.BlockControllerUrl;
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{bcUrl}/hubs/block-controller")
                .WithAutomaticReconnect()
                .Build();

            await _hubConnection.StartAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("EnvelopeSender connected to Block Controller hub");
            return _hubConnection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
