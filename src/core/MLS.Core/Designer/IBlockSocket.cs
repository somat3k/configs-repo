namespace MLS.Core.Designer;

/// <summary>
/// Represents a typed connection point on a <see cref="IBlockElement"/>.
/// Sockets with matching <see cref="BlockSocketType"/> values can be connected;
/// mismatched connections raise <see cref="InvalidBlockConnectionException"/>.
/// </summary>
public interface IBlockSocket
{
    /// <summary>Unique identifier of this socket instance.</summary>
    Guid SocketId { get; }

    /// <summary>
    /// Programmatic name, e.g. <c>candle_input</c> or <c>indicator_output</c>.
    /// Convention: <c>{type}_input[_{n}]</c> / <c>{type}_output[_{n}]</c>.
    /// </summary>
    string Name { get; }

    /// <summary>Data type flowing through this socket.</summary>
    BlockSocketType DataType { get; }

    /// <summary>Whether this socket receives or emits data.</summary>
    SocketDirection Direction { get; }

    /// <summary><see langword="true"/> when this socket is linked to exactly one peer socket.</summary>
    bool IsConnected { get; }

    /// <summary>The <see cref="IBlockSocket.SocketId"/> of the peer socket, or <see langword="null"/> when disconnected.</summary>
    Guid? ConnectedToSocketId { get; }
}
