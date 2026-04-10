using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MLS.Trader.Models;

namespace MLS.Trader.Persistence;

/// <summary>
/// Data-access layer for trader orders and positions.
/// All methods are scoped to a single <see cref="TraderDbContext"/> instance.
/// </summary>
public sealed class TradeRepository(TraderDbContext _db)
{
    // ── Orders ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new <see cref="TradeEntity"/> record.
    /// No-ops (catches unique constraint) if an order with the same
    /// <see cref="TradeEntity.ClientOrderId"/> already exists.
    /// </summary>
    public async Task InsertAsync(TradeEntity entity, CancellationToken ct)
    {
        _db.Orders.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraint(ex))
        {
            _db.Entry(entity).State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Updates the <see cref="TradeEntity.State"/> for the given <paramref name="clientOrderId"/>.
    /// Returns <see langword="true"/> when a row was updated.
    /// </summary>
    public async Task<bool> UpdateStateAsync(
        string clientOrderId, string newState, CancellationToken ct)
    {
        var entity = await _db.Orders
            .FirstOrDefaultAsync(o => o.ClientOrderId == clientOrderId, ct)
            .ConfigureAwait(false);

        if (entity is null) return false;

        entity.State     = newState;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Returns the <see cref="TradeEntity"/> for the given <paramref name="clientOrderId"/>, or <see langword="null"/>.</summary>
    public Task<TradeEntity?> FindAsync(string clientOrderId, CancellationToken ct) =>
        _db.Orders.AsNoTracking()
                  .FirstOrDefaultAsync(o => o.ClientOrderId == clientOrderId, ct);

    /// <summary>Streams all orders in an open or partially-filled state.</summary>
    public async IAsyncEnumerable<TradeEntity> GetOpenOrdersAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var openStates = new[] { nameof(TraderOrderState.Open), nameof(TraderOrderState.PartiallyFilled), nameof(TraderOrderState.Pending) };

        await foreach (var entity in _db.Orders
            .AsNoTracking()
            .Where(o => openStates.Contains(o.State))
            .AsAsyncEnumerable()
            .WithCancellation(ct)
            .ConfigureAwait(false))
        {
            yield return entity;
        }
    }

    // ── Positions ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts a position snapshot — inserts a new row when the (symbol, venue) pair
    /// does not exist yet; otherwise updates the existing row.
    /// </summary>
    public async Task UpsertPositionAsync(PositionEntity entity, CancellationToken ct)
    {
        var existing = await _db.Positions
            .FirstOrDefaultAsync(p => p.Symbol == entity.Symbol && p.Venue == entity.Venue, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.Positions.Add(entity);
        }
        else
        {
            existing.Direction        = entity.Direction;
            existing.Quantity         = entity.Quantity;
            existing.AverageEntryPrice = entity.AverageEntryPrice;
            existing.UnrealisedPnl    = entity.UnrealisedPnl;
            existing.UpdatedAt        = entity.UpdatedAt;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns all open position snapshots.</summary>
    public async Task<IReadOnlyList<PositionEntity>> GetAllPositionsAsync(CancellationToken ct) =>
        await _db.Positions.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsUniqueConstraint(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true ||
        ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
