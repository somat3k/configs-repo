namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>DATA_COLLECTION_START</c> — sent by Designer to data-layer
/// to initiate a Hydra feed collection job.
/// </summary>
/// <param name="Exchange">Exchange identifier, e.g. <c>"hyperliquid"</c>.</param>
/// <param name="Symbol">Trading symbol, e.g. <c>"BTC-PERP"</c>.</param>
/// <param name="Timeframe">Candle timeframe, e.g. <c>"5m"</c>, <c>"1h"</c>.</param>
/// <param name="From">Start of the historical collection range (UTC).</param>
public sealed record DataCollectionStartPayload(
    string Exchange,
    string Symbol,
    string Timeframe,
    DateTimeOffset From);
