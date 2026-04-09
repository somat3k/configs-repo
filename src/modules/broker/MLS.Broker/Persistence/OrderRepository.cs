using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MLS.Broker.Models;

namespace MLS.Broker.Persistence;

/// <summary>
/// Data-access helper for <see cref="OrderEntity"/> records in PostgreSQL.
/// </summary>
public sealed class OrderRepository(BrokerDbContext _db)
{
    /// <summary>
    /// Inserts a new order row.  Silently no-ops when the
    /// <see cref="OrderEntity.ClientOrderId"/> already exists (idempotency).
    /// </summary>
    public async Task InsertAsync(OrderEntity entity, CancellationToken ct = default)
    {
        var exists = await _db.Orders
            .AnyAsync(o => o.ClientOrderId == entity.ClientOrderId, ct)
            .ConfigureAwait(false);

        if (exists) return;

        _db.Orders.Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a state/fill update to an existing order identified by
    /// <paramref name="clientOrderId"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the row was found and updated.</returns>
    public async Task<bool> UpdateStateAsync(
        string clientOrderId,
        string newState,
        decimal filledQuantity,
        decimal? averagePrice,
        CancellationToken ct = default)
    {
        var entity = await _db.Orders
            .FirstOrDefaultAsync(o => o.ClientOrderId == clientOrderId, ct)
            .ConfigureAwait(false);

        if (entity is null) return false;

        entity.State          = newState;
        entity.FilledQuantity = filledQuantity;
        entity.AveragePrice   = averagePrice;
        entity.UpdatedAt      = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Returns the order with the given <paramref name="clientOrderId"/>, or <see langword="null"/>.</summary>
    public Task<OrderEntity?> FindAsync(string clientOrderId, CancellationToken ct = default)
        => _db.Orders.FirstOrDefaultAsync(o => o.ClientOrderId == clientOrderId, ct);

    /// <summary>Streams all orders in the Open or PartiallyFilled state.</summary>
    public async IAsyncEnumerable<OrderEntity> GetOpenOrdersAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var openStates = new[]
        {
            OrderState.Open.ToString(),
            OrderState.PartiallyFilled.ToString(),
        };

        var query = _db.Orders
            .Where(o => openStates.Contains(o.State))
            .AsAsyncEnumerable()
            .WithCancellation(ct);

        await foreach (var entity in query.ConfigureAwait(false))
            yield return entity;
    }

    /// <summary>
    /// Upserts a <see cref="PositionEntity"/> row keyed by (symbol, venue).
    /// </summary>
    public async Task UpsertPositionAsync(PositionEntity position, CancellationToken ct = default)
    {
        var existing = await _db.Positions
            .FirstOrDefaultAsync(p => p.Symbol == position.Symbol && p.Venue == position.Venue, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.Positions.Add(position);
        }
        else
        {
            existing.Side               = position.Side;
            existing.Quantity           = position.Quantity;
            existing.AverageEntryPrice  = position.AverageEntryPrice;
            existing.UnrealisedPnl      = position.UnrealisedPnl;
            existing.UpdatedAt          = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns the current position for the given symbol and venue, or <see langword="null"/>.</summary>
    public Task<PositionEntity?> GetPositionAsync(string symbol, string venue, CancellationToken ct = default)
        => _db.Positions.FirstOrDefaultAsync(p => p.Symbol == symbol && p.Venue == venue, ct);
}
