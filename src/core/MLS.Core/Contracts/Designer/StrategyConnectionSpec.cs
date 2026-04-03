namespace MLS.Core.Contracts.Designer;

/// <summary>JSON-serialisable specification of a socket connection between two blocks.</summary>
/// <param name="ConnectionId">Unique edge identifier.</param>
/// <param name="FromBlockId">Source block.</param>
/// <param name="FromSocket">Name of the output socket on the source block.</param>
/// <param name="ToBlockId">Destination block.</param>
/// <param name="ToSocket">Name of the input socket on the destination block.</param>
public sealed record StrategyConnectionSpec(
    string ConnectionId,
    Guid FromBlockId,
    string FromSocket,
    Guid ToBlockId,
    string ToSocket);
