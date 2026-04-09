using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MLS.Arbitrager.Configuration;
using MLS.Core.Constants;
using Npgsql;

namespace MLS.Arbitrager.Addresses;

/// <summary>
/// PostgreSQL-backed blockchain address book for the Arbitrager module.
/// Loads all contract addresses from the <c>blockchain_addresses</c> table at startup.
/// Keyed by chain_id = 42161 (Arbitrum One).
/// </summary>
/// <remarks>
/// Schema: <c>blockchain_addresses(address_key VARCHAR PRIMARY KEY, chain_id INT, address VARCHAR(42))</c>
/// </remarks>
public sealed class AddressBook : IArbitragerAddressBook
{
    private readonly string _connectionString;
    private readonly ILogger<AddressBook> _logger;
    private ConcurrentDictionary<BlockchainAddress, string> _addresses = new();

    /// <summary>Initialises a new <see cref="AddressBook"/>.</summary>
    public AddressBook(IOptions<ArbitragerOptions> options, ILogger<AddressBook> logger)
    {
        _connectionString = options.Value.PostgresConnectionString;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<BlockchainAddress, string> All => _addresses;

    /// <inheritdoc/>
    public async ValueTask<string> GetRouterAddressAsync(BlockchainAddress key, CancellationToken ct)
    {
        if (_addresses.TryGetValue(key, out var address))
            return address;

        // Lazy refresh on first miss
        await RefreshAsync(ct).ConfigureAwait(false);

        if (_addresses.TryGetValue(key, out address))
            return address;

        throw new KeyNotFoundException(
            $"Blockchain address '{key}' not found. Ensure blockchain_addresses table has an entry for key '{key}' on chain_id 42161.");
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken ct)
    {
        _logger.LogInformation("AddressBook: refreshing blockchain addresses from PostgreSQL...");

        var fresh  = new ConcurrentDictionary<BlockchainAddress, string>();
        var loaded = 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT address_key, address FROM blockchain_addresses WHERE chain_id = 42161";

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var keyStr  = reader.GetString(0);
            var addr    = reader.GetString(1);

            if (Enum.TryParse<BlockchainAddress>(keyStr, ignoreCase: true, out var key))
            {
                fresh[key] = addr;
                loaded++;
            }
            else
            {
                _logger.LogWarning("AddressBook: unknown key '{Key}' — skipping.",
                    Regex.Replace(keyStr.Length > 64 ? keyStr[..64] : keyStr, @"[^A-Za-z0-9_]", "_"));
            }
        }

        _addresses = fresh;
        _logger.LogInformation("AddressBook: loaded {Count} addresses.", loaded);
    }
}

/// <summary>
/// Hosted startup service that pre-loads the blockchain address book before the module
/// begins serving traffic.  A failed load is non-fatal — <see cref="IArbitragerAddressBook"/>
/// will retry lazily on first use.
/// </summary>
internal sealed class AddressBookStartupService(
    IArbitragerAddressBook addressBook,
    ILogger<AddressBookStartupService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await addressBook.RefreshAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AddressBook: failed to pre-load addresses at startup — will retry on first use.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
