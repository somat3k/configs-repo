using System.Collections.Concurrent;
using MLS.Core.Designer;

namespace MLS.Designer.Services;

/// <summary>
/// Concrete implementation of <see cref="ITransformationController"/>.
/// Routes <see cref="TransformationEnvelope"/> objects through named sub-divisions,
/// maintaining a per-sub-division ordered list of registered processing blocks
/// and an in-memory audit trail keyed by origin signal block ID.
/// </summary>
/// <remarks>
/// <para>Thread-safe: uses <see cref="ConcurrentDictionary"/> for both the block registry
/// and the history store.  Processing within a sub-division is sequential (registration order).</para>
/// <para>History entries are retained in-memory and are bounded to the last
/// <see cref="MaxHistoryEntries"/> origins to prevent unbounded growth in long-running sessions.</para>
/// </remarks>
public sealed class TransformationController : ITransformationController
{
    private const int MaxHistoryEntries = 1024;

    // sub-division name → ordered list of registered processing blocks
    private readonly ConcurrentDictionary<string, List<IBlockElement>> _divisions =
        new(StringComparer.Ordinal);

    // origin block ID → list of transformation units accumulated for that origin
    private readonly ConcurrentDictionary<Guid, List<TransformationUnit>> _history = new();

    // Eviction queue keeps track of insertion order for bounded history
    private readonly Queue<Guid> _historyOrder = new();
    private readonly object _historyLock = new();

    // ── ITransformationController ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public void RegisterBlock(string subDivision, IBlockElement block)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subDivision);
        ArgumentNullException.ThrowIfNull(block);

        _divisions.GetOrAdd(subDivision, _ => []).Add(block);
    }

    /// <inheritdoc/>
    public async ValueTask<TransformationEnvelope> RouteAsync(
        TransformationEnvelope envelope,
        string subDivision,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subDivision);
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_divisions.TryGetValue(subDivision, out var blocks) || blocks.Count == 0)
            return envelope;

        var current = envelope;

        foreach (var block in blocks)
        {
            ct.ThrowIfCancellationRequested();

            BlockSignal? output = null;

            // Intercept the block's OutputProduced event for this single signal pass
            Func<BlockSignal, CancellationToken, ValueTask> handler = (sig, _) =>
            {
                output = sig;
                return ValueTask.CompletedTask;
            };

            if (block is Blocks.BlockBase bb)
                bb.OutputProduced += handler;

            try
            {
                await block.ProcessAsync(current.Signal, ct).ConfigureAwait(false);
            }
            finally
            {
                if (block is Blocks.BlockBase bb2)
                    bb2.OutputProduced -= handler;
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
        if (_history.TryGetValue(originSignalId, out var units))
            return units.AsReadOnly();
        return Array.Empty<TransformationUnit>();
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
                    _history.TryRemove(evict, out _);
                }

                _historyOrder.Enqueue(originId);
                _history[originId] = [];
            }

            _history[originId].Add(unit);
        }
    }
}
