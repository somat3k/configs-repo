using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>BLOCK_SIGNAL</c> — typed data propagating between blocks
/// in a deployed strategy graph via the Block Controller subscription table.
/// </summary>
/// <param name="BlockId">Source block that emitted the signal.</param>
/// <param name="StrategyId">Owning strategy graph.</param>
/// <param name="SocketName">Name of the output socket on the source block.</param>
/// <param name="SocketType">Declared socket data type (must match both endpoints).</param>
/// <param name="Value">The serialised signal value.</param>
/// <param name="Timestamp">UTC signal creation time.</param>
public sealed record BlockSignalPayload(
    [property: JsonPropertyName("block_id")] Guid BlockId,
    [property: JsonPropertyName("strategy_id")] Guid StrategyId,
    [property: JsonPropertyName("socket_name")] string SocketName,
    [property: JsonPropertyName("socket_type")] string SocketType,
    [property: JsonPropertyName("value")] JsonElement Value,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
