using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLS.Core.Constants;
using MLS.Core.Designer;
using Npgsql;

namespace MLS.Designer.Exchanges;

/// <summary>
/// PostgreSQL-backed blockchain address book.
/// Loads all contract addresses from the <c>blockchain_addresses</c> table at startup.
/// Refreshed on demand when a <c>REGISTER_UPDATE</c> envelope is received.
/// Never caches hardcoded addresses — all values come from the database.
/// </summary>
/// <remarks>
/// Schema: <c>blockchain_addresses(address_key VARCHAR PRIMARY KEY, chain_id INT, address VARCHAR(42))</c>
/// </remarks>
public sealed class ExchangeRegistry : IBlockchainAddressBook, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<ExchangeRegistry> _logger;
    private ConcurrentDictionary<BlockchainAddress, string> _addresses = new();

    /// <summary>Initialises a new <see cref="ExchangeRegistry"/>.</summary>
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

        // Lazy load on first miss
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
        cmd.CommandText = "SELECT address_key, address FROM blockchain_addresses WHERE chain_id = 42161";
        // chain_id 42161 = Arbitrum One — the only chain supported by this platform instance

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

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _dataSource.Dispose();
        return ValueTask.CompletedTask;
    }
}
