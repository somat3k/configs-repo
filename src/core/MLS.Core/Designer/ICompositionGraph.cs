namespace MLS.Core.Designer;

/// <summary>
/// Nestable strategy container — equivalent to StockSharp's <c>CompositionDiagramElement</c>.
/// Disconnected inner sockets are exposed as outer ports via <see cref="GetExposedPorts"/>,
/// enabling unlimited fractal nesting of composite strategy blocks.
/// </summary>
public interface ICompositionGraph
{
    /// <summary>Unique identifier of this graph.</summary>
    Guid GraphId { get; }

    /// <summary>Human-readable strategy name.</summary>
    string Name { get; }

    /// <summary>
    /// Schema version. MUST be incremented on every structural change.
    /// Minor changes (parameter rename): +1. Breaking changes (socket removal, type change): +10.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>All block instances registered in this graph.</summary>
    IReadOnlyList<IBlockElement> Blocks { get; }

    /// <summary>All directed edges (connections) in this graph.</summary>
    IReadOnlyList<BlockConnection> Connections { get; }

    /// <summary>
    /// Returns all inner sockets that have no connection within this graph.
    /// These are exposed as the outer-facing ports of a composite block.
    /// </summary>
    IReadOnlyList<IBlockSocket> GetExposedPorts();

    /// <summary>Add a block instance to the graph.</summary>
    Task AddBlockAsync(IBlockElement block, CancellationToken ct);

    /// <summary>Remove a block and all its connections from the graph.</summary>
    Task RemoveBlockAsync(Guid blockId, CancellationToken ct);

    /// <summary>
    /// Connect an output socket to an input socket.
    /// </summary>
    /// <exception cref="InvalidBlockConnectionException">
    /// Thrown when socket types do not match.
    /// </exception>
    Task ConnectAsync(Guid fromSocketId, Guid toSocketId, CancellationToken ct);

    /// <summary>Remove a connection by its identifier.</summary>
    Task DisconnectAsync(Guid connectionId, CancellationToken ct);

    /// <summary>Emit a <c>STRATEGY_DEPLOY</c> envelope via Block Controller.</summary>
    Task DeployAsync(CancellationToken ct);

    /// <summary>Emit a <c>STRATEGY_STATE_CHANGE(Stopped)</c> envelope.</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Run the graph against historical data between <paramref name="from"/> and <paramref name="to"/>.</summary>
    Task BacktestAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
