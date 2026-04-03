namespace MLS.Core.Designer;

/// <summary>
/// Represents a typed connection point on a <see cref="IBlockElement"/>.
/// Sockets with matching <see cref="BlockSocketType"/> values can be connected;
/// mismatched connections raise <see cref="InvalidBlockConnectionException"/>.
/// </summary>
/// <remarks>
/// <b>Fan-out</b>: an output socket may be connected to multiple downstream input sockets
/// (one output → many inputs). <see cref="ConnectedSocketIds"/> is therefore a collection.
/// An input socket may also accept multiple upstream outputs (fan-in) for blocks such as
/// <c>EnsembleBlock</c>. The <see cref="ICompositionGraph"/> owns the authoritative edge list;
/// these properties are a convenience view maintained by the socket implementation.
/// </remarks>
public interface IBlockSocket
{
    /// <summary>Unique identifier of this socket instance.</summary>
    public Guid SocketId { get; }

    /// <summary>
    /// Programmatic name, e.g. <c>candle_input</c> or <c>indicator_output</c>.
    /// Convention: <c>{type}_input[_{n}]</c> / <c>{type}_output[_{n}]</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>Data type flowing through this socket.</summary>
    public BlockSocketType DataType { get; }

    /// <summary>Whether this socket receives or emits data.</summary>
    public SocketDirection Direction { get; }

    /// <summary><see langword="true"/> when this socket has at least one peer connection.</summary>
    public bool IsConnected { get; }

    /// <summary>
    /// The <see cref="SocketId"/> values of all peer sockets currently connected to this one.
    /// Empty when disconnected. Output sockets may have multiple entries (fan-out).
    /// Input sockets typically have one entry but may have more (fan-in blocks).
    /// </summary>
    public IReadOnlyList<Guid> ConnectedSocketIds { get; }
}
