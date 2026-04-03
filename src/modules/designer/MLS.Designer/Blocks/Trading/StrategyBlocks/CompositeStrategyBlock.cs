using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.StrategyBlocks;

/// <summary>
/// Nestable composite strategy block — implements both <see cref="IBlockElement"/>
/// and <see cref="ICompositionGraph"/> for fractal strategy composition.
/// Disconnected inner sockets are exposed as outer ports via <see cref="GetExposedPorts"/>.
/// </summary>
public sealed class CompositeStrategyBlock : BlockBase, ICompositionGraph
{
    private readonly List<IBlockElement>  _blocks      = [];
    private readonly List<BlockConnection> _connections = [];

    // Inner exposed sockets are dynamically derived from the inner graph
    private static readonly IReadOnlyList<IBlockSocket> _empty = [];

    /// <inheritdoc/>
    public Guid   GraphId       { get; } = Guid.NewGuid();
    /// <inheritdoc/>
    public string Name          { get; set; } = "Composite Strategy";
    /// <inheritdoc/>
    public int    SchemaVersion { get; private set; } = 1;

    /// <inheritdoc/>
    public override string BlockType   => "CompositeStrategyBlock";
    /// <inheritdoc/>
    public override string DisplayName => Name;
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [];

    /// <inheritdoc/>
    public IReadOnlyList<IBlockElement>   Blocks      => _blocks;
    /// <inheritdoc/>
    public IReadOnlyList<BlockConnection> Connections => _connections;

    /// <summary>Initialises a new <see cref="CompositeStrategyBlock"/>.</summary>
    public CompositeStrategyBlock() : base(_empty, _empty) { }

    // ── ICompositionGraph ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<IBlockSocket> GetExposedPorts()
    {
        var connectedSocketIds = new HashSet<Guid>(
            _connections.SelectMany(c => new[] { c.FromSocketId, c.ToSocketId }));

        return _blocks
            .SelectMany(b => b.InputSockets.Concat(b.OutputSockets))
            .Where(s => !connectedSocketIds.Contains(s.SocketId))
            .ToList();
    }

    /// <inheritdoc/>
    public Task AddBlockAsync(IBlockElement block, CancellationToken ct)
    {
        _blocks.Add(block);
        SchemaVersion++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveBlockAsync(Guid blockId, CancellationToken ct)
    {
        _blocks.RemoveAll(b => b.BlockId == blockId);
        _connections.RemoveAll(c => c.FromBlockId == blockId || c.ToBlockId == blockId);
        SchemaVersion++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ConnectAsync(Guid fromSocketId, Guid toSocketId, CancellationToken ct)
    {
        var from = FindSocket(fromSocketId);
        var to   = FindSocket(toSocketId);

        if (from is null || to is null)
            throw new InvalidOperationException($"Socket {fromSocketId} or {toSocketId} not found.");

        BlockConnectionValidator.Validate(from, to);

        _connections.Add(BlockConnection.Create(
            FindBlockId(fromSocketId), from.SocketId,
            FindBlockId(toSocketId),   to.SocketId,
            from.DataType));
        SchemaVersion++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(Guid connectionId, CancellationToken ct)
    {
        _connections.RemoveAll(c => c.ConnectionId == connectionId);
        SchemaVersion++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeployAsync(CancellationToken ct) => Task.CompletedTask;
    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct)   => Task.CompletedTask;
    /// <inheritdoc/>
    public Task BacktestAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => Task.CompletedTask;

    // ── IBlockElement ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override void Reset() => _blocks.ForEach(b => b.Reset());

    /// <inheritdoc/>
    protected override async ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        // Route the signal into the inner graph by finding the right entry block
        foreach (var block in _blocks)
            await block.ProcessAsync(signal, ct).ConfigureAwait(false);
        return null;
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        foreach (var block in _blocks)
            await block.DisposeAsync().ConfigureAwait(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private IBlockSocket? FindSocket(Guid socketId) =>
        _blocks.SelectMany(b => b.InputSockets.Concat(b.OutputSockets))
               .FirstOrDefault(s => s.SocketId == socketId);

    private Guid FindBlockId(Guid socketId) =>
        _blocks.FirstOrDefault(b =>
            b.InputSockets.Any(s => s.SocketId == socketId) ||
            b.OutputSockets.Any(s => s.SocketId == socketId))?.BlockId ?? Guid.Empty;
}
