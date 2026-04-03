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
    Guid GraphId,
    string Name,
    int SchemaVersion,
    IReadOnlyList<StrategyBlockSpec> Blocks,
    IReadOnlyList<StrategyConnectionSpec> Connections);
