using MLS.Core.Constants;

namespace MLS.Core.Designer;

/// <summary>
/// Provides resolved blockchain contract addresses keyed by <see cref="BlockchainAddress"/>.
/// Implementations load all addresses from PostgreSQL at startup and cache them in memory.
/// No hardcoded addresses are permitted anywhere in the platform.
/// </summary>
public interface IBlockchainAddressBook
{
    /// <summary>
    /// Returns the EVM contract address string for the given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The address enum key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The 0x-prefixed address string.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when <paramref name="key"/> is not found in the address book.</exception>
    ValueTask<string> GetAddressAsync(BlockchainAddress key, CancellationToken ct);

    /// <summary>
    /// Returns all addresses as a read-only dictionary.
    /// </summary>
    IReadOnlyDictionary<BlockchainAddress, string> All { get; }

    /// <summary>Reloads all addresses from the backing store (called on REGISTER_UPDATE envelope).</summary>
    Task RefreshAsync(CancellationToken ct);
}
