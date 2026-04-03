using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>STRATEGY_DEPLOY</c> — complete graph definition sent to Block Controller
/// which updates the subscription table so live envelopes follow designer connections.
/// </summary>
/// <param name="GraphId">Strategy graph identifier.</param>
/// <param name="Name">Human-readable strategy name.</param>
/// <param name="SchemaVersion">Incremented on every structural change.</param>
/// <param name="Blocks">All block specifications in the graph.</param>
/// <param name="Connections">All directed socket connections.</param>
public sealed record StrategyGraphPayload(
    [property: JsonPropertyName("graph_id")]       Guid GraphId,
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("blocks")]         IReadOnlyList<StrategyBlockSpec> Blocks,
    [property: JsonPropertyName("connections")]    IReadOnlyList<StrategyConnectionSpec> Connections);
