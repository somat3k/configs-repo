using System.Text.Json;
using MLS.Core.Designer;

namespace MLS.Designer.Blocks;

/// <summary>
/// Base class for all MLS block implementations.
/// Handles socket wiring, output routing, and the <see cref="IAsyncDisposable"/> contract.
/// Subclasses implement <see cref="ProcessCoreAsync"/> and declare sockets + parameters.
/// </summary>
public abstract class BlockBase : IBlockElement
{
    /// <inheritdoc/>
    public Guid BlockId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public abstract string BlockType { get; }

    /// <inheritdoc/>
    public abstract string DisplayName { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IBlockSocket> InputSockets { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IBlockSocket> OutputSockets { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<BlockParameter> Parameters { get; }

    // ── Output signal routing ─────────────────────────────────────────────────────

    /// <summary>
    /// Raised when this block produces an output signal.
    /// Downstream blocks register to this event via the composition graph.
    /// </summary>
    public event Func<BlockSignal, CancellationToken, ValueTask>? OutputProduced;

    /// <summary>Initialises the block with its socket layout.</summary>
    protected BlockBase(
        IReadOnlyList<IBlockSocket> inputSockets,
        IReadOnlyList<IBlockSocket> outputSockets)
    {
        InputSockets  = inputSockets;
        OutputSockets = outputSockets;
    }

    // ── IBlockElement ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct)
    {
        var output = await ProcessCoreAsync(signal, ct).ConfigureAwait(false);
        if (output.HasValue && OutputProduced is not null)
            await OutputProduced(output.Value, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task PreloadAsync(IEnumerable<BlockSignal> historicalData, CancellationToken ct)
    {
        Reset();
        foreach (var signal in historicalData)
            await ProcessCoreAsync(signal, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public abstract void Reset();

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Abstract core ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Core processing logic. Return a signal to emit downstream, or <c>null</c>
    /// when the block needs more data before it can produce an output (e.g. warm-up).
    /// Implementations should minimise per-signal allocations on the hot path.
    /// Note: the <see cref="EmitFloat"/> and <see cref="EmitObject"/> helpers use
    /// <see cref="System.Text.Json.JsonSerializer"/> internally; override emission if
    /// zero-allocation is required.
    /// </summary>
    protected abstract ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct);

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Emit a float value on the first output socket.</summary>
    protected static BlockSignal EmitFloat(Guid blockId, string socketName, BlockSocketType socketType, float value) =>
        new(blockId, socketName, socketType, JsonSerializer.SerializeToElement(value));

    /// <summary>Emit an object value on the first output socket.</summary>
    protected static BlockSignal EmitObject<T>(Guid blockId, string socketName, BlockSocketType socketType, T value)
        where T : notnull =>
        new(blockId, socketName, socketType, JsonSerializer.SerializeToElement(value));
}
