using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.ShellVM.Hubs;
using StackExchange.Redis;

namespace MLS.ShellVM.Services;

/// <summary>
/// Broadcasts real-time output chunks to WebSocket subscribers via SignalR groups.
/// Also appends chunks to the per-session Redis ring-buffer for late-joining clients.
/// </summary>
public sealed class OutputBroadcaster(
    IHubContext<ShellVMHub> _hub,
    IConnectionMultiplexer? _redis,
    IOptions<ShellVMConfig> _config,
    ILogger<OutputBroadcaster> _logger) : IOutputBroadcaster
{
    // connectionId → ConcurrentDictionary<sessionId, byte> used as a concurrent set.
    // A single connection may subscribe to multiple sessions simultaneously.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _subscriptions = new();

    /// <inheritdoc/>
    public async ValueTask BroadcastChunkAsync(OutputChunk chunk, CancellationToken ct)
    {
        var payload = new ShellOutputPayload(
            SessionId: chunk.SessionId.ToString(),   // shell session ID for downstream correlation
            Stream:    chunk.Stream.ToString().ToLowerInvariant(),
            Chunk:     chunk.Data,
            Sequence:  chunk.Sequence,
            Timestamp: chunk.Timestamp.ToString("O"));

        var envelope = EnvelopePayload.Create(
            MessageTypes.ShellOutput,
            ShellVMNetworkConstants.ModuleName,
            payload);

        // Fan-out to all SignalR subscribers of this session
        var group = SessionGroup(chunk.SessionId);
        await _hub.Clients.Group(group)
                  .SendAsync("ReceiveOutput", envelope, ct)
                  .ConfigureAwait(false);

        // Append to Redis ring-buffer
        await AppendToRingBufferAsync(chunk, payload, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(string connectionId, Guid sessionId, CancellationToken ct)
    {
        var sessions = _subscriptions.GetOrAdd(connectionId, _ => new ConcurrentDictionary<Guid, byte>());
        sessions.TryAdd(sessionId, 0);

        await _hub.Groups.AddToGroupAsync(connectionId, SessionGroup(sessionId), ct)
                  .ConfigureAwait(false);
        _logger.LogDebug("Connection {ConnId} subscribed to session {SessionId}", connectionId, sessionId);
    }

    /// <inheritdoc/>
    public async Task UnsubscribeAsync(string connectionId, CancellationToken ct)
    {
        if (!_subscriptions.TryRemove(connectionId, out var sessions))
            return;

        foreach (var (sessionId, _) in sessions)
        {
            await _hub.Groups.RemoveFromGroupAsync(connectionId, SessionGroup(sessionId), ct)
                      .ConfigureAwait(false);
        }
        _logger.LogDebug("Connection {ConnId} unsubscribed from all sessions", connectionId);
    }

    /// <inheritdoc/>
    public async Task UnsubscribeAsync(string connectionId, Guid sessionId, CancellationToken ct)
    {
        if (!_subscriptions.TryGetValue(connectionId, out var sessions))
            return;

        if (!sessions.TryRemove(sessionId, out _))
            return;

        await _hub.Groups.RemoveFromGroupAsync(connectionId, SessionGroup(sessionId), ct)
                  .ConfigureAwait(false);
        _logger.LogDebug("Connection {ConnId} unsubscribed from session {SessionId}", connectionId, sessionId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string SessionGroup(Guid sessionId) => $"session:{sessionId}";

    private async Task AppendToRingBufferAsync(
        OutputChunk chunk, ShellOutputPayload payload, CancellationToken ct)
    {
        if (_redis is null) return;
        try
        {
            var cfg          = _config.Value;
            var ringCapacity = Math.Clamp(cfg.OutputRingBufferLines, 1, ShellVMLimits.RedisRingBufferLines);

            var db  = _redis.GetDatabase();
            var key = $"{ShellVMLimits.RedisOutputPrefix}{chunk.SessionId}";
            var json = JsonSerializer.Serialize(payload);
            await db.ListRightPushAsync(key, json).ConfigureAwait(false);
            await db.ListTrimAsync(key, -ringCapacity, -1).ConfigureAwait(false);
            await db.KeyExpireAsync(key, ShellVMLimits.SessionRedisTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append chunk to Redis ring-buffer for session {SessionId}",
                chunk.SessionId);
        }
    }
}

