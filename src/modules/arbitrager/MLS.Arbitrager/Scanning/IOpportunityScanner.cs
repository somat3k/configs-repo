using System.Threading.Channels;

namespace MLS.Arbitrager.Scanning;

/// <summary>
/// Scans multiple exchanges for price discrepancies and surfaces profitable arbitrage paths.
/// </summary>
public interface IOpportunityScanner
{
    /// <summary>
    /// Channel reader that yields detected <see cref="ArbitrageOpportunity"/> instances.
    /// Bounded by <c>ArbitragerOptions.OpportunityQueueCapacity</c>; oldest item is dropped
    /// when the channel is full.
    /// </summary>
    ChannelReader<ArbitrageOpportunity> Opportunities { get; }

    /// <summary>
    /// Inject a fresh <see cref="PriceSnapshot"/> into the scanner graph.
    /// Called by price-feed background workers; must be allocation-free on the hot path.
    /// </summary>
    void PublishPrice(PriceSnapshot snapshot);

    /// <summary>Returns all active price snapshots keyed by <c>"exchange/symbol"</c>.</summary>
    IReadOnlyDictionary<string, PriceSnapshot> GetCurrentPrices();
}
