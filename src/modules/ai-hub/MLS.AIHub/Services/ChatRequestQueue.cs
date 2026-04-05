using System.Threading.Channels;
using Microsoft.Extensions.Options;
using MLS.AIHub.Configuration;
using MLS.Core.Contracts.Designer;

namespace MLS.AIHub.Services;

/// <summary>
/// A single queued AI chat request, produced by <see cref="Controllers.ChatController"/>
/// and consumed by <see cref="ChatQueueProcessor"/>.
/// </summary>
/// <param name="Query">The validated query payload.</param>
/// <param name="UserId">Target user — SignalR responses are routed to this ID's group.</param>
/// <param name="CancellationToken">Caller-supplied cancellation (e.g. HTTP disconnect).</param>
public sealed record ChatQueueItem(
    AiQueryPayload Query,
    Guid UserId,
    CancellationToken CancellationToken);

/// <summary>
/// Bounded queue for AI chat requests — decouples HTTP acceptance from AI processing
/// and prevents unbounded thread-pool growth under load.
/// </summary>
public interface IChatRequestQueue
{
    /// <summary>
    /// Attempts to enqueue a chat request for background processing.
    /// Returns <see langword="false"/> when the queue is at capacity
    /// (caller should respond with HTTP 429).
    /// </summary>
    bool TryEnqueue(ChatQueueItem item);
}

/// <summary>
/// Hosted service that consumes <see cref="ChatQueueItem"/> entries from a
/// bounded <see cref="Channel{T}"/> and processes each via a scoped <see cref="IChatService"/>.
/// Also implements <see cref="IChatRequestQueue"/> so controllers can enqueue work.
/// </summary>
/// <remarks>
/// Channel capacity is controlled by <see cref="AIHubOptions.ChatQueueCapacity"/> (default 256).
/// When the channel is full <see cref="TryEnqueue"/> returns <see langword="false"/> immediately
/// (non-blocking, no work is dropped silently).
/// </remarks>
public sealed class ChatQueueProcessor(
    IServiceScopeFactory _scopeFactory,
    IOptions<AIHubOptions> _options,
    ILogger<ChatQueueProcessor> _logger) : BackgroundService, IChatRequestQueue
{
    private readonly Channel<ChatQueueItem> _channel = Channel.CreateBounded<ChatQueueItem>(
        new BoundedChannelOptions(_options.Value.ChatQueueCapacity)
        {
            FullMode     = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    // ── IChatRequestQueue ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool TryEnqueue(ChatQueueItem item) => _channel.Writer.TryWrite(item);

    // ── BackgroundService ─────────────────────────────────────────────────────

    /// <summary>
    /// Reads items from the bounded channel and processes each in its own DI scope,
    /// linking the item's caller cancellation token with the host stopping token.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChatQueueProcessor started (capacity={Capacity})",
            _options.Value.ChatQueueCapacity);

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            using var scope      = _scopeFactory.CreateScope();
            var chatService      = scope.ServiceProvider.GetRequiredService<IChatService>();

            // Link caller CT (e.g. HTTP disconnect) + host stopping token
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                item.CancellationToken, stoppingToken);

            try
            {
                await chatService.ProcessQueryAsync(item.Query, item.UserId, cts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug(
                    "ChatQueueProcessor: request for userId {UserId} was cancelled", item.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ChatQueueProcessor: unhandled error for userId {UserId}", item.UserId);
            }
        }

        _logger.LogInformation("ChatQueueProcessor stopped");
    }
}
