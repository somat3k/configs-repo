using MLS.Core.Designer;

namespace MLS.WebApp.Components.Designer;

/// <summary>View model for a block instance on the designer canvas.</summary>
public sealed class BlockViewModel(Guid blockId, string blockType, double x, double y,
    List<SocketViewModel> inputSockets, List<SocketViewModel> outputSockets)
{
    public Guid BlockId { get; } = blockId;
    public string BlockType { get; } = blockType;
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public List<SocketViewModel> InputSockets { get; } = inputSockets;
    public List<SocketViewModel> OutputSockets { get; } = outputSockets;
}

/// <summary>View model for a socket on a block node.</summary>
public sealed record SocketViewModel(Guid SocketId, string Name, BlockSocketType SocketType);

/// <summary>View model for a connection (wire) between two sockets.</summary>
public sealed record ConnectionViewModel(
    Guid ConnectionId,
    Guid FromBlockId,
    Guid FromSocketId,
    Guid ToBlockId,
    Guid ToSocketId);

/// <summary>Operational health state of a block node.</summary>
public enum BlockStatus
{
    /// <summary>Block is processing messages normally.</summary>
    Active,
    /// <summary>Block is slow or seeing elevated error rate.</summary>
    Degraded,
    /// <summary>Block has exceeded the error threshold.</summary>
    Error,
}

/// <summary>
/// Live per-block runtime metrics tracked on the designer canvas.
/// All fields are updated from BLOCK_SIGNAL envelopes and reset each 1-second window.
/// Thread-safe: <see cref="RecordSignal"/> may be called from the envelope-consumer thread
/// while <see cref="Flush"/> is called from the 1-Hz timer thread.
/// </summary>
public sealed class BlockMetrics
{
    private readonly object _lock = new();

    // ── Published display values (written inside lock, read on flush result) ─
    /// <summary>Rolling average inference latency over the last 50 samples (ms).</summary>
    public double LatencyMs { get; private set; }

    /// <summary>Message throughput — signals processed in the last complete second.</summary>
    public double MessagesPerSecond { get; private set; }

    /// <summary>Error ratio over the last 60-second sliding window (0–100).</summary>
    public double ErrorRatePct { get; private set; }

    /// <summary>Derived operational status.</summary>
    public BlockStatus Status { get; private set; } = BlockStatus.Active;

    // ── Internal accumulators (guarded by _lock) ──────────────────────────────
    private int _pendingMsgCount;
    private int _pendingErrCount;

    // Rolling latency — last 50 samples
    private readonly Queue<double> _latencyWindow = new();
    private double _latencySum;

    // 60-second sliding window for error rate — stored as a compact ring of timestamps
    // Running totals track the queue counts so Flush() is O(expired-entries) not O(n).
    private readonly Queue<(DateTimeOffset At, bool IsError)> _sliding60 = new();
    private int _sliding60Total;
    private int _sliding60Errors;

    /// <summary>
    /// Records one processed signal for the current second window and the 60-second
    /// error-rate window. Thread-safe; may be called concurrently from the consumer loop.
    /// </summary>
    /// <param name="latencyMs">Round-trip latency for this signal in milliseconds.</param>
    /// <param name="isError">Whether this signal represents a block error.</param>
    public void RecordSignal(double latencyMs, bool isError)
    {
        lock (_lock)
        {
            _pendingMsgCount++;
            if (isError) _pendingErrCount++;

            // Rolling latency (last 50 samples)
            _latencyWindow.Enqueue(latencyMs);
            _latencySum += latencyMs;
            if (_latencyWindow.Count > 50)
                _latencySum -= _latencyWindow.Dequeue();

            // 60-second sliding window — O(1) running totals
            var now = DateTimeOffset.UtcNow;
            _sliding60.Enqueue((now, isError));
            _sliding60Total++;
            if (isError) _sliding60Errors++;

            // Evict entries older than 60 s
            while (_sliding60.Count > 0 && (now - _sliding60.Peek().At).TotalSeconds > 60)
            {
                var evicted = _sliding60.Dequeue();
                _sliding60Total--;
                if (evicted.IsError) _sliding60Errors--;
            }
        }
    }

    /// <summary>
    /// Flushes accumulated counts into the published display properties and resets
    /// the per-second window. Call once per second from the display timer.
    /// Thread-safe; safe to call while <see cref="RecordSignal"/> runs concurrently.
    /// </summary>
    public void Flush()
    {
        lock (_lock)
        {
            MessagesPerSecond = _pendingMsgCount;
            _pendingMsgCount = 0;
            _pendingErrCount = 0;

            LatencyMs = _latencyWindow.Count > 0 ? _latencySum / _latencyWindow.Count : 0;

            ErrorRatePct = _sliding60Total > 0
                ? (double)_sliding60Errors / _sliding60Total * 100.0
                : 0;
        }

        Status = ErrorRatePct >= 20
            ? BlockStatus.Error
            : MessagesPerSecond == 0 && LatencyMs == 0
                ? BlockStatus.Active          // idle — no data yet, show green
                : ErrorRatePct >= 5
                    ? BlockStatus.Degraded
                    : BlockStatus.Active;
    }
}
