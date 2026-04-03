using System.Text.Json;

namespace MLS.Core.Designer;

/// <summary>
/// Typed data packet flowing between connected <see cref="IBlockSocket"/> instances.
/// Carries the socket name, socket type, raw value, and a UTC timestamp.
/// </summary>
/// <param name="SourceBlockId">The block that emitted this signal.</param>
/// <param name="SourceSocketName">The output socket name on the source block.</param>
/// <param name="SocketType">Runtime data type — must match the socket's declared <see cref="BlockSocketType"/>.</param>
/// <param name="Value">
/// The signal payload serialised as a <see cref="JsonElement"/> for zero-copy forwarding.
/// Consumers deserialise to the concrete type implied by <paramref name="SocketType"/>.
/// </param>
/// <param name="Timestamp">UTC creation time.</param>
public readonly record struct BlockSignal(
    Guid SourceBlockId,
    string SourceSocketName,
    BlockSocketType SocketType,
    JsonElement Value,
    DateTimeOffset Timestamp)
{
    /// <summary>Initialises a signal with the current UTC time.</summary>
    public BlockSignal(Guid sourceBlockId, string sourceSocketName, BlockSocketType socketType, JsonElement value)
        : this(sourceBlockId, sourceSocketName, socketType, value, DateTimeOffset.UtcNow) { }
}
