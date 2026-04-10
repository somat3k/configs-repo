namespace MLS.DeFi.Addresses;

/// <summary>
/// Provides blockchain contract and wallet addresses stored in the PostgreSQL
/// <c>blockchain_addresses</c> table. All addresses are loaded by name — no
/// hardcoded strings are permitted anywhere in the module.
/// </summary>
public interface IDeFiAddressBook
{
    /// <summary>
    /// Returns the blockchain address registered under <paramref name="name"/>,
    /// or <see langword="null"/> when the name is not found.
    /// </summary>
    Task<string?> GetAddressAsync(string name, CancellationToken ct);

    /// <summary>
    /// Returns all registered addresses as a dictionary keyed by name.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAddressesAsync(CancellationToken ct);

    /// <summary>
    /// Reloads the address book from the database, invalidating any cached values.
    /// </summary>
    Task ReloadAsync(CancellationToken ct);
}
