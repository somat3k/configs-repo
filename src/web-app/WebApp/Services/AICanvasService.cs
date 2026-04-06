using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using MLS.WebApp.Components.Canvas;

namespace MLS.WebApp.Services;

/// <summary>
/// Canvas action recorded when the AI Hub dispatches an <c>AI_CANVAS_ACTION</c> envelope.
/// </summary>
/// <param name="ActionType">Canvas action type (e.g. <c>OpenPanel</c>, <c>ShowDiagram</c>).</param>
/// <param name="PanelType">Target panel type for open-panel actions.</param>
/// <param name="Title">Optional window title for new MDI panels.</param>
/// <param name="WindowId">MDI window ID when a panel was opened; otherwise <see langword="null"/>.</param>
/// <param name="Timestamp">UTC timestamp when the action was received.</param>
public sealed record CanvasActionRecord(
    string ActionType,
    string? PanelType,
    string? Title,
    Guid? WindowId,
    DateTimeOffset Timestamp);

/// <summary>
/// Manages the SignalR connection to the AI Hub module, streams response chunks
/// to the chat panel, and dispatches canvas actions to the MDI <see cref="WindowManager"/>.
/// </summary>
public interface IAICanvasService : IAsyncDisposable
{
    /// <summary><see langword="true"/> when the SignalR connection to ai-hub is open.</summary>
    bool IsConnected { get; }

    /// <summary>Fired whenever response chunks, canvas actions, or connection state changes.</summary>
    event Action? OnStateChanged;

    /// <summary>Recent canvas actions dispatched during the current session (newest last).</summary>
    IReadOnlyList<CanvasActionRecord> RecentActions { get; }

    /// <summary>Ensures the SignalR connection to ai-hub is established.</summary>
    Task EnsureConnectedAsync(CancellationToken ct);

    /// <summary>
    /// Sends an <c>AI_QUERY</c> envelope to the AI Hub and opens a new response stream.
    /// Call <see cref="ReadChunksAsync"/> immediately after to consume the streaming reply.
    /// </summary>
    /// <param name="query">The user's natural-language message.</param>
    /// <param name="providerOverride">Optional: force a specific LLM provider ID.</param>
    /// <param name="modelOverride">Optional: force a specific model identifier.</param>
    /// <param name="history">Prior conversation turns serialised as JSON elements.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendQueryAsync(
        string query,
        string? providerOverride,
        string? modelOverride,
        IReadOnlyList<JsonElement> history,
        CancellationToken ct);

    /// <summary>
    /// Returns an async stream of response chunks for the most-recently sent query.
    /// The stream ends when a chunk with <see cref="AiResponseChunkPayload.IsFinal"/> is received
    /// or the token is cancelled.
    /// </summary>
    IAsyncEnumerable<AiResponseChunkPayload> ReadChunksAsync(CancellationToken ct);
}

/// <inheritdoc cref="IAICanvasService"/>
public sealed class AICanvasService(
    IConfiguration configuration,
    WindowManager windowManager,
    ILogger<AICanvasService> logger) : IAICanvasService
{
    private HubConnection? _connection;
    private readonly Guid _clientId = Guid.NewGuid();
    private Channel<AiResponseChunkPayload> _chunks = CreateResponseChannel();

    private readonly List<CanvasActionRecord> _recentActions = [];
    private readonly object _lock = new();

    /// <inheritdoc/>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc/>
    public event Action? OnStateChanged;

    /// <inheritdoc/>
    public IReadOnlyList<CanvasActionRecord> RecentActions
    {
        get { lock (_lock) { return _recentActions.ToList().AsReadOnly(); } }
    }

    // ── Connection ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connection is not null
            && _connection.State != HubConnectionState.Disconnected)
            return;

        var baseUrl = configuration["MLS:Network:AIHubUrl"] ?? "http://ai-hub:5750";
        var url = $"{baseUrl}/hubs/ai-hub?clientId={_clientId}";

        _connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<EnvelopePayload>("ReceiveEnvelope", HandleEnvelope);

        _connection.Reconnected += _ =>
        {
            logger.LogInformation("AIHub hub reconnected (clientId={ClientId})", _clientId);
            return Task.CompletedTask;
        };

        _connection.Closed += ex =>
        {
            logger.LogWarning(ex, "AIHub hub connection closed (clientId={ClientId})", _clientId);
            OnStateChanged?.Invoke();
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Connected to AI Hub at {Url}", url);
        OnStateChanged?.Invoke();
    }

    // ── Query ─────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendQueryAsync(
        string query,
        string? providerOverride,
        string? modelOverride,
        IReadOnlyList<JsonElement> history,
        CancellationToken ct)
    {
        await EnsureConnectedAsync(ct).ConfigureAwait(false);

        // Reset response channel so the new stream is isolated.
        // The lock prevents a race between SendQueryAsync and HandleEnvelope.
        Channel<AiResponseChunkPayload> oldChannel;
        Channel<AiResponseChunkPayload> newChannel = CreateResponseChannel();
        lock (_lock)
        {
            oldChannel = _chunks;
            _chunks = newChannel;
        }
        oldChannel.Writer.TryComplete();

        var payload = new AiQueryPayload(
            Query: query,
            ProviderOverride: providerOverride,
            ModelOverride: modelOverride,
            IncludeCanvasContext: true,
            ConversationHistory: history);

        var envelope = EnvelopePayload.Create(
            MessageTypes.AiQuery,
            "web-app",
            payload);

        await _connection!.InvokeAsync("SendEnvelope", envelope, ct).ConfigureAwait(false);
        logger.LogDebug("Sent AI_QUERY (clientId={ClientId})", _clientId);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<AiResponseChunkPayload> ReadChunksAsync(CancellationToken ct)
    {
        // Capture the current channel under lock to avoid reading from a stale instance.
        Channel<AiResponseChunkPayload> channel;
        lock (_lock) { channel = _chunks; }
        return channel.Reader.ReadAllAsync(ct);
    }

    // ── Envelope handler ──────────────────────────────────────────────────────────

    private void HandleEnvelope(EnvelopePayload envelope)
    {
        switch (envelope.Type)
        {
            case MessageTypes.AiResponseChunk:
                var chunk = envelope.Payload.Deserialize<AiResponseChunkPayload>(JsonOptions);
                if (chunk is not null)
                {
                    Channel<AiResponseChunkPayload> target;
                    lock (_lock) { target = _chunks; }
                    target.Writer.TryWrite(chunk);
                    if (chunk.IsFinal)
                        target.Writer.TryComplete();
                }
                break;

            case MessageTypes.AiCanvasAction:
                var action = envelope.Payload.Deserialize<AiCanvasActionPayload>(JsonOptions);
                if (action is not null)
                    HandleCanvasAction(action);
                break;

            case MessageTypes.AiResponseComplete:
                Channel<AiResponseChunkPayload> toComplete;
                lock (_lock) { toComplete = _chunks; }
                toComplete.Writer.TryComplete();
                break;
        }

        OnStateChanged?.Invoke();
    }

    private void HandleCanvasAction(AiCanvasActionPayload action)
    {
        Guid? windowId = null;

        if (action.ActionType == "OpenPanel" && action.PanelType is not null)
        {
            windowId = windowManager.OpenPanel(
                action.PanelType,
                action.Title,
                x: 120,
                y: 80,
                width: 860,
                height: 560);

            logger.LogDebug(
                "AI canvas: opened panel {PanelType} (winId={WinId})",
                action.PanelType, windowId);
        }

        lock (_lock)
        {
            _recentActions.Add(new CanvasActionRecord(
                ActionType: action.ActionType,
                PanelType: action.PanelType,
                Title: action.Title,
                WindowId: windowId,
                Timestamp: DateTimeOffset.UtcNow));
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _chunks.Writer.TryComplete();
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Channel<AiResponseChunkPayload> CreateResponseChannel()
        => Channel.CreateBounded<AiResponseChunkPayload>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
