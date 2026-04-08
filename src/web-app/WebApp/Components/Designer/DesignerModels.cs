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
/// </summary>
public sealed class BlockMetrics
{
    // ── Published display values ─────────────────────────────────────────────
    /// <summary>Rolling average inference latency over the last 100 ms window (ms).</summary>
    public double LatencyMs { get; set; }

    /// <summary>Message throughput — signals processed in the last complete second.</summary>
    public double MessagesPerSecond { get; set; }

    /// <summary>Error ratio over the last 60-second sliding window (0–100).</summary>
    public double ErrorRatePct { get; set; }

    /// <summary>Derived operational status.</summary>
    public BlockStatus Status { get; set; } = BlockStatus.Active;

    // ── Internal accumulators (reset every second) ───────────────────────────
    internal int PendingMsgCount;
    internal int PendingErrCount;
    internal readonly Queue<(DateTimeOffset At, bool IsError)> SlidingWindow60s = new();
    internal readonly Queue<double> LatencyWindow = new();
    internal DateTimeOffset WindowStart = DateTimeOffset.UtcNow;

    /// <summary>
    /// Records one processed signal for the current second window and the 60-second
    /// error-rate window. Call this on every BLOCK_SIGNAL received for this block.
    /// </summary>
    /// <param name="latencyMs">Round-trip latency for this signal in milliseconds.</param>
    /// <param name="isError">Whether this signal represents a block error.</param>
    public void RecordSignal(double latencyMs, bool isError)
    {
        PendingMsgCount++;
        if (isError) PendingErrCount++;

        LatencyWindow.Enqueue(latencyMs);
        while (LatencyWindow.Count > 50) LatencyWindow.Dequeue();

        var now = DateTimeOffset.UtcNow;
        SlidingWindow60s.Enqueue((now, isError));
        while (SlidingWindow60s.Count > 0 && (now - SlidingWindow60s.Peek().At).TotalSeconds > 60)
            SlidingWindow60s.Dequeue();
    }

    /// <summary>
    /// Flushes accumulated counts into the published display properties and resets
    /// the per-second window. Call once per second from the display timer.
    /// </summary>
    public void Flush()
    {
        MessagesPerSecond = PendingMsgCount;
        PendingMsgCount = 0;
        PendingErrCount = 0;
        WindowStart = DateTimeOffset.UtcNow;

        LatencyMs = LatencyWindow.Count > 0 ? LatencyWindow.Average() : 0;

        var total = SlidingWindow60s.Count;
        var errors = SlidingWindow60s.Count(e => e.IsError);
        ErrorRatePct = total > 0 ? (double)errors / total * 100.0 : 0;

        Status = ErrorRatePct >= 20
            ? BlockStatus.Error
            : MessagesPerSecond == 0 && LatencyMs == 0
                ? BlockStatus.Active          // idle — no data yet, show green
                : ErrorRatePct >= 5
                    ? BlockStatus.Degraded
                    : BlockStatus.Active;
    }
}
