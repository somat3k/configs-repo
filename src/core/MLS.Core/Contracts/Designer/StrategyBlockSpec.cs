using System.Text.Json;

namespace MLS.Core.Contracts.Designer;

/// <summary>JSON-serialisable specification of a single block within a strategy graph.</summary>
/// <param name="BlockId">Unique block instance identifier.</param>
/// <param name="BlockType">Registry key, e.g. <c>"RSIBlock"</c>.</param>
/// <param name="Parameters">Block-specific configuration key/value pairs.</param>
public sealed record StrategyBlockSpec(
    Guid BlockId,
    string BlockType,
    JsonElement Parameters);
