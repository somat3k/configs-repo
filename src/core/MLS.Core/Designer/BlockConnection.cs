namespace MLS.Core.Designer;

/// <summary>
/// Represents an immutable directed edge in a <see cref="ICompositionGraph"/>
/// linking an output socket on one block to an input socket on another.
/// </summary>
/// <param name="ConnectionId">Unique edge identifier.</param>
/// <param name="FromBlockId">Source block.</param>
/// <param name="FromSocketId">Output socket on the source block.</param>
/// <param name="ToBlockId">Destination block.</param>
/// <param name="ToSocketId">Input socket on the destination block.</param>
/// <param name="SocketType">
/// Shared socket type validated at connection time.
/// Both endpoints must declare the same <see cref="BlockSocketType"/>.
/// </param>
public sealed record BlockConnection(
    Guid ConnectionId,
    Guid FromBlockId,
    Guid FromSocketId,
    Guid ToBlockId,
    Guid ToSocketId,
    BlockSocketType SocketType)
{
    /// <summary>Creates a new <see cref="BlockConnection"/> with a generated <see cref="ConnectionId"/>.</summary>
    public static BlockConnection Create(
        Guid fromBlockId, Guid fromSocketId,
        Guid toBlockId, Guid toSocketId,
        BlockSocketType socketType) =>
        new(Guid.NewGuid(), fromBlockId, fromSocketId, toBlockId, toSocketId, socketType);
}
