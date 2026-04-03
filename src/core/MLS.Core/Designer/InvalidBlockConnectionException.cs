namespace MLS.Core.Designer;

/// <summary>
/// Thrown by <see cref="ICompositionGraph.ConnectAsync"/> when a connection attempt
/// violates the socket type system — i.e. the source and destination sockets
/// declare incompatible <see cref="BlockSocketType"/> values.
/// </summary>
public sealed class InvalidBlockConnectionException : Exception
{
    /// <summary>The socket type declared by the source (output) socket.</summary>
    public BlockSocketType FromType { get; }

    /// <summary>The socket type declared by the destination (input) socket.</summary>
    public BlockSocketType ToType { get; }

    /// <inheritdoc/>
    public InvalidBlockConnectionException(
        Guid fromSocketId,
        BlockSocketType fromType,
        Guid toSocketId,
        BlockSocketType toType)
        : base(
            $"Cannot connect socket {fromSocketId} ({fromType}) to socket {toSocketId} ({toType}): " +
            "socket types do not match.")
    {
        FromType = fromType;
        ToType   = toType;
    }

    /// <inheritdoc/>
    public InvalidBlockConnectionException(string message) : base(message) { }

    /// <inheritdoc/>
    public InvalidBlockConnectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
