using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;
using MLS.Broker.Persistence;
using StackExchange.Redis;

namespace MLS.Broker.Services;

/// <summary>
/// Tracks in-flight and completed orders with a PostgreSQL primary store and
/// a Redis hot cache for low-latency clientOrderId lookups.
/// </summary>
public sealed class OrderTracker(
    IDbContextFactory<BrokerDbContext> _dbFactory,
    IConnectionMultiplexer _redis,
    ILogger<OrderTracker> _logger) : IOrderTracker
{
    private static readonly TimeSpan RedisTtl = TimeSpan.FromHours(48);

    /// <inheritdoc/>
    public async Task TrackAsync(PlaceOrderRequest request, OrderResult result, CancellationToken ct)
    {
        // Persist to PostgreSQL first — source of truth
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new OrderRepository(db);
        await repo.InsertAsync(MapToEntity(request, result), ct).ConfigureAwait(false);

        // Warm Redis cache after a successful DB write
        await CacheOrderAsync(result, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        string clientOrderId,
        OrderState newState,
        decimal filledQuantity,
        decimal? averagePrice,
        CancellationToken ct)
    {
        // Update Redis
        var cached = await GetCachedAsync(clientOrderId, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            var updated = cached with
            {
                State          = newState,
                FilledQuantity = filledQuantity,
                AveragePrice   = averagePrice,
                UpdatedAt      = DateTimeOffset.UtcNow,
            };
            await CacheOrderAsync(updated, ct).ConfigureAwait(false);
        }

        // Persist update to PostgreSQL
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new OrderRepository(db);
        var updated_pg = await repo.UpdateStateAsync(
            clientOrderId, newState.ToString(), filledQuantity, averagePrice, ct)
            .ConfigureAwait(false);

        if (!updated_pg)
            _logger.LogWarning("OrderTracker: no row found to update for {ClientOrderId}", BrokerUtils.SafeLog(clientOrderId));
    }

    /// <inheritdoc/>
    public async Task<OrderResult?> GetAsync(string clientOrderId, CancellationToken ct)
    {
        // Hot path: Redis cache
        var cached = await GetCachedAsync(clientOrderId, ct).ConfigureAwait(false);
        if (cached is not null) return cached;

        // Cold path: PostgreSQL
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo   = new OrderRepository(db);
        var entity = await repo.FindAsync(clientOrderId, ct).ConfigureAwait(false);

        if (entity is null) return null;

        var result = MapFromEntity(entity);
        await CacheOrderAsync(result, ct).ConfigureAwait(false); // warm cache
        return result;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<OrderResult> GetOpenOrdersAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new OrderRepository(db);

        await foreach (var entity in repo.GetOpenOrdersAsync(ct).ConfigureAwait(false))
            yield return MapFromEntity(entity);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task CacheOrderAsync(OrderResult order, CancellationToken ct)
    {
        try
        {
            var db  = _redis.GetDatabase();
            var key = $"broker:order:{order.ClientOrderId}";
            var val = JsonSerializer.Serialize(order);
            await db.StringSetAsync(key, val, RedisTtl).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis cache write failed for {ClientOrderId}", BrokerUtils.SafeLog(order.ClientOrderId));
        }
    }

    private async Task<OrderResult?> GetCachedAsync(string clientOrderId, CancellationToken ct)
    {
        try
        {
            var db  = _redis.GetDatabase();
            var key = $"broker:order:{clientOrderId}";
            var val = await db.StringGetAsync(key).ConfigureAwait(false);

            if (val.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<OrderResult>(val.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis cache read failed for {ClientOrderId}", BrokerUtils.SafeLog(clientOrderId));
            return null;
        }
    }

    private static OrderEntity MapToEntity(PlaceOrderRequest req, OrderResult result) => new()
    {
        ClientOrderId      = result.ClientOrderId,
        VenueOrderId       = result.VenueOrderId,
        Symbol             = req.Symbol,
        Side               = req.Side.ToString(),
        OrderType          = req.Type.ToString(),
        Quantity           = req.Quantity,
        LimitPrice         = req.LimitPrice,
        StopPrice          = req.StopPrice,
        State              = result.State.ToString(),
        FilledQuantity     = result.FilledQuantity,
        AveragePrice       = result.AveragePrice,
        Venue              = result.Venue,
        RequestingModuleId = req.RequestingModuleId,
        CreatedAt          = result.CreatedAt,
        UpdatedAt          = result.UpdatedAt,
    };

    private static OrderResult MapFromEntity(OrderEntity e) => new(
        ClientOrderId:  e.ClientOrderId,
        VenueOrderId:   e.VenueOrderId,
        State:          Enum.TryParse<OrderState>(e.State, out var s) ? s : OrderState.Pending,
        FilledQuantity: e.FilledQuantity,
        AveragePrice:   e.AveragePrice,
        Venue:          e.Venue,
        Symbol:         e.Symbol,
        Side:           Enum.TryParse<OrderSide>(e.Side, out var side) ? side : OrderSide.Buy,
        CreatedAt:      e.CreatedAt,
        UpdatedAt:      e.UpdatedAt);
}
