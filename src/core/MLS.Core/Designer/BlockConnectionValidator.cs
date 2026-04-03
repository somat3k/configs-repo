namespace MLS.Core.Designer;

/// <summary>
/// Validates socket connections before they are committed to a <see cref="ICompositionGraph"/>.
/// Implementations of <see cref="ICompositionGraph.ConnectAsync"/> MUST call
/// <see cref="Validate"/> before creating the edge.
/// </summary>
public static class BlockConnectionValidator
{
    /// <summary>
    /// Validates that two sockets are compatible for connection.
    /// </summary>
    /// <param name="from">The output socket (source).</param>
    /// <param name="to">The input socket (destination).</param>
    /// <exception cref="InvalidBlockConnectionException">
    /// When socket types do not match or direction constraints are violated.
    /// </exception>
    public static void Validate(IBlockSocket from, IBlockSocket to)
    {
        if (from.Direction != SocketDirection.Output)
            throw new InvalidBlockConnectionException(
                $"Socket '{from.Name}' ({from.SocketId}) must be an Output socket to be a connection source.");

        if (to.Direction != SocketDirection.Input)
            throw new InvalidBlockConnectionException(
                $"Socket '{to.Name}' ({to.SocketId}) must be an Input socket to be a connection destination.");

        if (from.DataType != to.DataType)
            throw new InvalidBlockConnectionException(
                from.SocketId, from.DataType,
                to.SocketId, to.DataType);
    }
}
