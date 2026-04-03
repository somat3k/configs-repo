using MLS.Core.Designer;

namespace MLS.Designer.Blocks;

/// <summary>
/// Concrete implementation of <see cref="IBlockSocket"/> used by all block implementations.
/// Sockets are immutable by design — connections are tracked by the graph, not the socket.
/// </summary>
internal sealed class BlockSocket(
    Guid socketId,
    string name,
    BlockSocketType dataType,
    SocketDirection direction) : IBlockSocket
{
    private readonly List<Guid> _connectedSocketIds = [];

    /// <inheritdoc/>
    public Guid SocketId { get; } = socketId;

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public BlockSocketType DataType { get; } = dataType;

    /// <inheritdoc/>
    public SocketDirection Direction { get; } = direction;

    /// <inheritdoc/>
    public bool IsConnected => _connectedSocketIds.Count > 0;

    /// <inheritdoc/>
    public IReadOnlyList<Guid> ConnectedSocketIds => _connectedSocketIds;

    /// <summary>Record a connection to the specified peer socket.</summary>
    internal void ConnectTo(Guid connectedSocketId)
    {
        if (connectedSocketId == Guid.Empty)
            throw new ArgumentException("Connected socket ID cannot be empty.", nameof(connectedSocketId));
        if (!_connectedSocketIds.Contains(connectedSocketId))
            _connectedSocketIds.Add(connectedSocketId);
    }

    /// <summary>Remove a connection to the specified peer socket.</summary>
    /// <returns><see langword="true"/> if the connection was tracked and removed.</returns>
    internal bool DisconnectFrom(Guid connectedSocketId)
    {
        if (connectedSocketId == Guid.Empty)
            throw new ArgumentException("Connected socket ID cannot be empty.", nameof(connectedSocketId));
        return _connectedSocketIds.Remove(connectedSocketId);
    }

    /// <summary>Removes all tracked peer socket connections.</summary>
    internal void ClearConnections() => _connectedSocketIds.Clear();

    // ── Factory helpers ───────────────────────────────────────────────────────────

    /// <summary>Create an input socket with a generated ID.</summary>
    public static BlockSocket Input(string name, BlockSocketType type) =>
        new(Guid.NewGuid(), name, type, SocketDirection.Input);

    /// <summary>Create an output socket with a generated ID.</summary>
    public static BlockSocket Output(string name, BlockSocketType type) =>
        new(Guid.NewGuid(), name, type, SocketDirection.Output);
}
