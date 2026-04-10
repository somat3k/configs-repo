using MLS.Arbitrager.Scanning;

namespace MLS.Arbitrager.Execution;

/// <summary>
/// Builds an ordered <see cref="TransactionArray"/> from a scored
/// <see cref="ArbitrageOpportunity"/>.
/// Resolves all on-chain router addresses from the blockchain address book and
/// computes slippage-adjusted minimum output amounts.
/// </summary>
public interface IArrayBuilder
{
    /// <summary>
    /// Constructs a <see cref="TransactionArray"/> for the given opportunity.
    /// Each hop becomes one <see cref="TransactionStep"/> with the router address
    /// resolved from PostgreSQL via <see cref="Addresses.IArbitragerAddressBook"/>.
    /// </summary>
    /// <param name="opportunity">The scored arbitrage opportunity.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<TransactionArray> BuildAsync(ArbitrageOpportunity opportunity, CancellationToken ct);
}
