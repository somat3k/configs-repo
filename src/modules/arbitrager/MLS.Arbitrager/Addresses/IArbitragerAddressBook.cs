using MLS.Core.Constants;

namespace MLS.Arbitrager.Addresses;

/// <summary>
/// Provides resolved blockchain contract addresses used by the Arbitrager module.
/// All values are loaded from PostgreSQL — no hardcoded addresses permitted.
/// </summary>
public interface IArbitragerAddressBook
{
    /// <summary>
    /// Returns the EVM router/contract address string for the given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The address enum key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the address book.</exception>
    ValueTask<string> GetRouterAddressAsync(BlockchainAddress key, CancellationToken ct);

    /// <summary>Returns all loaded addresses as a read-only dictionary.</summary>
    IReadOnlyDictionary<BlockchainAddress, string> All { get; }

    /// <summary>Reloads all addresses from PostgreSQL.</summary>
    Task RefreshAsync(CancellationToken ct);
}
