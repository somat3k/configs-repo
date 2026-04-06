using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Contracts;

namespace MLS.WebApp.Services;

/// <summary>
/// SignalR client that connects to the Block Controller hub and fans-out incoming
/// envelopes to multiple topic-filtered <see cref="IAsyncEnumerable{T}"/> consumers.
/// </summary>
public sealed class BlockControllerHub(
    IConfiguration configuration,
    ILogger<BlockControllerHub> logger) : IBlockControllerHub, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly Channel<EnvelopePayload> _envelopes =
        Channel.CreateBounded<EnvelopePayload>(new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
        });
    private readonly Channel<ModuleStatusUpdate> _moduleUpdates =
        Channel.CreateBounded<ModuleStatusUpdate>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
        });

    /// <inheritdoc />
    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct)
    {
        var url = configuration["MLS:Network:BlockControllerWsUrl"]
                  ?? "http://block-controller:6100/hubs/block-controller";

        _connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<EnvelopePayload>("ReceiveEnvelope", env =>
            _envelopes.Writer.TryWrite(env));

        _connection.On<ModuleStatusUpdate>("ReceiveModuleUpdate", update =>
            _moduleUpdates.Writer.TryWrite(update));

        _connection.Reconnected += _ =>
        {
            logger.LogInformation("BlockController hub reconnected");
            return Task.CompletedTask;
        };

        _connection.Closed += ex =>
        {
            logger.LogWarning(ex, "BlockController hub connection closed");
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Connected to Block Controller at {Url}", url);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken ct)
    {
        if (_connection is not null)
            await _connection.StopAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EnvelopePayload> GetEnvelopeStreamAsync(
        string[] topics,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var topicSet = new HashSet<string>(topics, StringComparer.OrdinalIgnoreCase);
        await foreach (var env in _envelopes.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (topicSet.Count == 0 || topicSet.Contains(env.Type))
                yield return env;
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ModuleStatusUpdate> GetModuleUpdatesAsync(CancellationToken ct)
        => _moduleUpdates.Reader.ReadAllAsync(ct);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _envelopes.Writer.TryComplete();
        _moduleUpdates.Writer.TryComplete();
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
