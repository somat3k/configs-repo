using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>JSON-serialisable specification of a single block within a strategy graph.</summary>
/// <param name="BlockId">Unique block instance identifier.</param>
/// <param name="BlockType">Registry key, e.g. <c>"RSIBlock"</c>.</param>
/// <param name="Parameters">Block-specific configuration key/value pairs.</param>
public sealed record StrategyBlockSpec(
    [property: JsonPropertyName("block_id")]   Guid BlockId,
    [property: JsonPropertyName("block_type")] string BlockType,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);
