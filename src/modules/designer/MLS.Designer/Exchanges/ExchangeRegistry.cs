using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Constants;
using MLS.Core.Designer;
using Npgsql;

namespace MLS.Designer.Exchanges;

/// <summary>
/// PostgreSQL-backed blockchain address book.
/// All contract addresses are loaded from the <c>blockchain_addresses</c> table.
/// Call <see cref="RefreshAsync"/> at startup (via <see cref="ExchangeRegistryStartupService"/>)
/// to pre-populate the cache; <see cref="GetAddressAsync"/> falls back to a lazy load if the
/// cache is empty.
/// Never stores hardcoded addresses — all values come from the database.
/// </summary>
/// <remarks>
/// Schema: <c>blockchain_addresses(address_key VARCHAR PRIMARY KEY, chain_id INT, address VARCHAR(42))</c>
/// </remarks>
public sealed class ExchangeRegistry : IBlockchainAddressBook
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<ExchangeRegistry> _logger;
    private ConcurrentDictionary<BlockchainAddress, string> _addresses = new();

    /// <summary>Initialises a new <see cref="ExchangeRegistry"/>.</summary>
    /// <remarks>
    /// The <paramref name="dataSource"/> is owned by the DI container — this class does
    /// <b>not</b> dispose it.
    /// </remarks>
    public ExchangeRegistry(NpgsqlDataSource dataSource, ILogger<ExchangeRegistry> logger)
    {
        _dataSource = dataSource;
        _logger     = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<BlockchainAddress, string> All => _addresses;

    /// <inheritdoc/>
    public async ValueTask<string> GetAddressAsync(BlockchainAddress key, CancellationToken ct)
    {
        if (_addresses.TryGetValue(key, out var address))
            return address;

        // Lazy load on first miss (safety net if startup service hasn't run yet)
        await RefreshAsync(ct).ConfigureAwait(false);

        if (_addresses.TryGetValue(key, out address))
            return address;

        throw new KeyNotFoundException(
            $"Blockchain address '{key}' not found. Verify the blockchain_addresses table contains an entry for key '{key}'.");
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken ct)
    {
        _logger.LogInformation("Refreshing blockchain address book from PostgreSQL...");

        var fresh = new ConcurrentDictionary<BlockchainAddress, string>();
        var loaded = 0;

        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd  = conn.CreateCommand();
        // chain_id 42161 = Arbitrum One — the only chain supported by this platform instance
        cmd.CommandText = "SELECT address_key, address FROM blockchain_addresses WHERE chain_id = 42161";

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var keyStr  = reader.GetString(0);
            var address = reader.GetString(1);

            if (Enum.TryParse<BlockchainAddress>(keyStr, ignoreCase: true, out var key))
            {
                fresh[key] = address;
                loaded++;
            }
            else
            {
                _logger.LogWarning("Unknown blockchain_address key '{Key}' in PostgreSQL — skipping.", keyStr);
            }
        }

        _addresses = fresh;
        _logger.LogInformation("Blockchain address book loaded: {Count} addresses.", loaded);
    }
}

/// <summary>
/// Hosted service that loads all blockchain addresses from PostgreSQL when the Designer
/// module starts. Ensures adapters never operate with an empty address book.
/// </summary>
internal sealed class ExchangeRegistryStartupService(
    IBlockchainAddressBook addressBook,
    ILogger<ExchangeRegistryStartupService> logger) : IHostedService
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
            // Non-fatal: GetAddressAsync will retry lazily on first use.
            logger.LogError(ex, "ExchangeRegistry: failed to pre-load blockchain addresses at startup.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
