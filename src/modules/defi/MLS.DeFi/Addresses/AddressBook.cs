using System.Collections.Concurrent;
using MLS.DeFi.Configuration;
using Npgsql;

namespace MLS.DeFi.Addresses;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IDeFiAddressBook"/>.
/// Loads all addresses from the <c>blockchain_addresses</c> table on first use and
/// caches them in memory. Use <see cref="ReloadAsync"/> to invalidate the cache.
/// </summary>
public sealed class AddressBook(
    IOptions<DeFiOptions> _options,
    ILogger<AddressBook> _logger) : IDeFiAddressBook, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyDictionary<string, string>? _cache;

    /// <inheritdoc/>
    public async Task<string?> GetAddressAsync(string name, CancellationToken ct)
    {
        var all = await EnsureLoadedAsync(ct).ConfigureAwait(false);
        return all.TryGetValue(name, out var addr) ? addr : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAddressesAsync(CancellationToken ct)
        => await EnsureLoadedAsync(ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cache = null;
            _cache = await LoadFromDatabaseAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<IReadOnlyDictionary<string, string>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache is not null)
                return _cache;

            _cache = await LoadFromDatabaseAsync(ct).ConfigureAwait(false);
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadFromDatabaseAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(_options.Value.PostgresConnectionString);
        try
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT name, address FROM blockchain_addresses WHERE active = true";

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var name    = reader.GetString(0);
                var address = reader.GetString(1);
                result[name] = address;
            }

            _logger.LogInformation("Address book loaded {Count} entries from blockchain_addresses", result.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load blockchain_addresses — returning empty address book");
        }

        return result;
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();
}
