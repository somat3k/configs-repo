using System.Collections.Concurrent;
using System.Collections.Immutable;
using MLS.Core.Designer;

namespace MLS.Designer.Services;

/// <summary>
/// Concrete implementation of <see cref="ITransformationController"/>.
/// Routes <see cref="TransformationEnvelope"/> objects through named sub-divisions,
/// maintaining a per-sub-division ordered list of registered processing blocks
/// and an in-memory audit trail keyed by origin signal block ID.
/// </summary>
/// <remarks>
/// <para>Thread-safe: <c>_divisions</c> uses an immutable snapshot swap (ImmutableArray +
/// Interlocked exchange) so <see cref="RegisterBlock"/> and <see cref="RouteAsync"/>
/// never race.  <c>_history</c> is guarded by <c>_historyLock</c> for all reads and writes.</para>
/// <para>History entries are bounded to the last
/// <see cref="MaxHistoryEntries"/> origins to prevent unbounded growth in long-running sessions.</para>
/// </remarks>
public sealed class TransformationController : ITransformationController
{
    private const int MaxHistoryEntries = 1024;

    // sub-division name → immutable array snapshot of registered blocks (lock-free reads)
    private readonly ConcurrentDictionary<string, ImmutableArray<IBlockElement>> _divisions =
        new(StringComparer.Ordinal);

    // per-sub-division locks used only by RegisterBlock to safely build new snapshots
    private readonly ConcurrentDictionary<string, object> _divisionLocks =
        new(StringComparer.Ordinal);

    // origin block ID → list of transformation units accumulated for that origin
    private readonly Dictionary<Guid, List<TransformationUnit>> _history = new();

    // Eviction queue keeps track of insertion order for bounded history
    private readonly Queue<Guid> _historyOrder = new();
    private readonly object _historyLock = new();

    // ── ITransformationController ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public void RegisterBlock(string subDivision, IBlockElement block)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subDivision);
        ArgumentNullException.ThrowIfNull(block);

        var divLock = _divisionLocks.GetOrAdd(subDivision, _ => new object());
        lock (divLock)
        {
            var current = _divisions.GetOrAdd(subDivision, _ => ImmutableArray<IBlockElement>.Empty);
            // Atomically replace the snapshot with a new one that includes the new block
            _divisions[subDivision] = current.Add(block);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TransformationEnvelope> RouteAsync(
        TransformationEnvelope envelope,
        string subDivision,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subDivision);
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_divisions.TryGetValue(subDivision, out var blocks) || blocks.IsEmpty)
            return envelope;

        var current = envelope;

        // Snapshot is an ImmutableArray — safe to enumerate without a lock
        foreach (var block in blocks)
        {
            ct.ThrowIfCancellationRequested();

            BlockSignal? output = null;

            // Subscribe to capture the first output emitted during this specific ProcessAsync call.
            // The handler is subscribed and unsubscribed within the same synchronous frame around
            // the await, preventing stale captures from asynchronous background emissions.
            Func<BlockSignal, CancellationToken, ValueTask> handler = (sig, _) =>
            {
                // Capture only the first emission (last-wins would drop earlier outputs).
                // For multi-output blocks (e.g. RouterBlock) the TransformationController
                // is not the right routing vehicle; use those blocks' own output sockets directly.
                output ??= sig;
                return ValueTask.CompletedTask;
            };

            if (block is Blocks.BlockBase blockBase)
                blockBase.OutputProduced += handler;

            try
            {
                await block.ProcessAsync(current.Signal, ct).ConfigureAwait(false);
            }
            finally
            {
                if (block is Blocks.BlockBase blockBase2)
                    blockBase2.OutputProduced -= handler;
            }

            if (output.HasValue)
            {
                var unit = new TransformationUnit(
                    BlockId:     block.BlockId,
                    BlockType:   block.BlockType,
                    SubDivision: subDivision,
                    AppliedAt:   DateTimeOffset.UtcNow);

                current = current.WithTransformation(unit);
                current = current with { Signal = output.Value };

                // Record in history
                RecordHistory(envelope.OriginBlockId, unit);
            }
        }

        return current;
    }

    /// <inheritdoc/>
    public IReadOnlyList<TransformationUnit> GetHistory(Guid originSignalId)
    {
        lock (_historyLock)
        {
            if (_history.TryGetValue(originSignalId, out var units))
                return units.ToArray();  // Return immutable copy under lock
            return Array.Empty<TransformationUnit>();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private void RecordHistory(Guid originId, TransformationUnit unit)
    {
        if (originId == Guid.Empty)
            return;

        lock (_historyLock)
        {
            if (!_history.ContainsKey(originId))
            {
                // Evict oldest entry when at capacity
                if (_historyOrder.Count >= MaxHistoryEntries)
                {
                    var evict = _historyOrder.Dequeue();
                    _history.Remove(evict);
                }

                _historyOrder.Enqueue(originId);
                _history[originId] = [];
            }

            _history[originId].Add(unit);
        }
    }
}
