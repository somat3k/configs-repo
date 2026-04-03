using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>JSON-serialisable specification of a socket connection between two blocks.</summary>
/// <param name="ConnectionId">Unique edge identifier.</param>
/// <param name="FromBlockId">Source block.</param>
/// <param name="FromSocket">Name of the output socket on the source block.</param>
/// <param name="ToBlockId">Destination block.</param>
/// <param name="ToSocket">Name of the input socket on the destination block.</param>
public sealed record StrategyConnectionSpec(
    [property: JsonPropertyName("connection_id")] string ConnectionId,
    [property: JsonPropertyName("from_block_id")] Guid FromBlockId,
    [property: JsonPropertyName("from_socket")] string FromSocket,
    [property: JsonPropertyName("to_block_id")] Guid ToBlockId,
    [property: JsonPropertyName("to_socket")] string ToSocket);
