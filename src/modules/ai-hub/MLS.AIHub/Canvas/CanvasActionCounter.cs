namespace MLS.AIHub.Canvas;

/// <summary>
/// Tracks the number of <c>AI_CANVAS_ACTION</c> envelopes dispatched during a single
/// AI response pipeline execution (scoped per request).
/// </summary>
public interface ICanvasActionCounter
{
    /// <summary>Atomically increments the counter by one.</summary>
    void Increment();

    /// <summary>Current dispatch count.</summary>
    int Count { get; }
}

/// <summary>
/// Scoped, thread-safe implementation of <see cref="ICanvasActionCounter"/>.
/// Uses lock-free atomic operations to track canvas action dispatches within a single AI response.
/// </summary>
public sealed class CanvasActionCounter : ICanvasActionCounter
{
    private int _count;

    /// <inheritdoc/>
    public void Increment() => Interlocked.Increment(ref _count);

    /// <inheritdoc/>
    public int Count => Volatile.Read(ref _count);
}
