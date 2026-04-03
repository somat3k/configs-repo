namespace MLS.Core.Designer;

/// <summary>
/// Thrown by <see cref="ICompositionGraph.ConnectAsync"/> when a connection attempt
/// violates the socket type system — i.e. the source and destination sockets
/// declare incompatible <see cref="BlockSocketType"/> values, or direction constraints
/// (output → input) are violated.
/// </summary>
public sealed class InvalidBlockConnectionException : Exception
{
    /// <summary>
    /// The socket type declared by the source (output) socket, or <see langword="null"/>
    /// when the exception was raised for a direction violation rather than a type mismatch.
    /// </summary>
    public BlockSocketType? FromType { get; }

    /// <summary>
    /// The socket type declared by the destination (input) socket, or <see langword="null"/>
    /// when the exception was raised for a direction violation rather than a type mismatch.
    /// </summary>
    public BlockSocketType? ToType { get; }

    /// <summary>
    /// Creates an exception describing a socket <em>type mismatch</em>.
    /// Both <see cref="FromType"/> and <see cref="ToType"/> are populated.
    /// </summary>
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

    /// <summary>
    /// Creates an exception with a custom message (e.g. for direction violations).
    /// <see cref="FromType"/> and <see cref="ToType"/> remain <see langword="null"/>.
    /// </summary>
    public InvalidBlockConnectionException(string message) : base(message) { }

    /// <summary>
    /// Creates an exception with a custom message and inner exception.
    /// <see cref="FromType"/> and <see cref="ToType"/> remain <see langword="null"/>.
    /// </summary>
    public InvalidBlockConnectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
