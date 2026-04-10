using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MLS.DeFi.Models;
using Npgsql;

namespace MLS.DeFi.Persistence;

/// <summary>
/// Data-access helper for <see cref="TransactionEntity"/> records in PostgreSQL.
/// </summary>
public sealed class TransactionRepository(DeFiDbContext _db)
{
    /// <summary>
    /// Inserts a new transaction row.  Silently no-ops when the
    /// <see cref="TransactionEntity.ClientOrderId"/> already exists (idempotency).
    /// </summary>
    public async Task InsertAsync(TransactionEntity entity, CancellationToken ct = default)
    {
        var exists = await _db.Transactions
            .AnyAsync(t => t.ClientOrderId == entity.ClientOrderId, ct)
            .ConfigureAwait(false);

        if (exists) return;

        _db.Transactions.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.Entry(entity).State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Applies a state/fill update to an existing transaction row.
    /// </summary>
    /// <returns><see langword="true"/> when the row was found and updated.</returns>
    public async Task<bool> UpdateStateAsync(
        string clientOrderId,
        string newState,
        decimal filledQuantity,
        decimal? averagePrice,
        CancellationToken ct = default)
    {
        var entity = await _db.Transactions
            .FirstOrDefaultAsync(t => t.ClientOrderId == clientOrderId, ct)
            .ConfigureAwait(false);

        if (entity is null) return false;

        entity.State          = newState;
        entity.FilledQuantity = filledQuantity;
        entity.AveragePrice   = averagePrice;
        entity.UpdatedAt      = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Returns a transaction by its <paramref name="clientOrderId"/>, or
    /// <see langword="null"/> when not found.
    /// </summary>
    public Task<TransactionEntity?> GetByClientOrderIdAsync(string clientOrderId, CancellationToken ct = default)
        => _db.Transactions
              .AsNoTracking()
              .FirstOrDefaultAsync(t => t.ClientOrderId == clientOrderId, ct);

    /// <summary>
    /// Streams all transactions in the <c>Pending</c>, <c>Open</c>, or
    /// <c>PartiallyFilled</c> state.
    /// </summary>
    public async IAsyncEnumerable<TransactionEntity> GetOpenTransactionsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var openStates = new[]
        {
            DeFiOrderState.Pending.ToString(),
            DeFiOrderState.Open.ToString(),
            DeFiOrderState.PartiallyFilled.ToString(),
        };

        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => openStates.Contains(t.State))
            .AsAsyncEnumerable();

        await foreach (var tx in query.ConfigureAwait(false).WithCancellation(ct))
            yield return tx;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pg && pg.SqlState == "23505";
}
