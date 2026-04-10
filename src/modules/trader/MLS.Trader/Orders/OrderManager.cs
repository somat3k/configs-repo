using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Trader.Interfaces;
using MLS.Trader.Models;
using MLS.Trader.Persistence;
using MLS.Trader.Services;

namespace MLS.Trader.Orders;

/// <summary>
/// Manages the lifecycle of trader-owned orders.
/// Maintains an in-memory <see cref="ConcurrentDictionary{TKey,TValue}"/> as a hot cache
/// and persists all state to PostgreSQL via <see cref="TradeRepository"/>.
/// </summary>
public sealed class OrderManager(
    IDbContextFactory<TraderDbContext> _dbFactory,
    IEnvelopeSender _sender,
    ILogger<OrderManager> _logger) : IOrderManager
{
    private const string ModuleId = "trader";

    private readonly ConcurrentDictionary<string, TraderOrder> _cache = new();

    /// <inheritdoc/>
    public async Task<TraderOrder> CreateOrderAsync(
        string          symbol,
        SignalDirection direction,
        decimal         quantity,
        decimal         entryPrice,
        decimal         stopLossPrice,
        decimal         takeProfitPrice,
        bool            paperTrading,
        CancellationToken ct)
    {
        var clientOrderId = Guid.NewGuid().ToString();
        var now           = DateTimeOffset.UtcNow;

        var initialState = paperTrading ? TraderOrderState.Filled : TraderOrderState.Draft;

        var order = new TraderOrder(
            ClientOrderId:  clientOrderId,
            Symbol:         symbol,
            Direction:      direction,
            Quantity:       quantity,
            EntryPrice:     entryPrice,
            StopLossPrice:  stopLossPrice,
            TakeProfitPrice: takeProfitPrice,
            State:          initialState,
            PaperTrading:   paperTrading,
            CreatedAt:      now,
            UpdatedAt:      now);

        _cache[clientOrderId] = order;

        // Persist to PostgreSQL
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new TradeRepository(db);
        await repo.InsertAsync(MapToEntity(order), ct).ConfigureAwait(false);

        if (!paperTrading)
        {
            // Transition to Pending and dispatch ORDER_CREATE envelope
            order = order with { State = TraderOrderState.Pending, UpdatedAt = DateTimeOffset.UtcNow };
            _cache[clientOrderId] = order;
            await repo.UpdateStateAsync(clientOrderId, nameof(TraderOrderState.Pending), ct).ConfigureAwait(false);

            var payload = new
            {
                symbol              = symbol,
                side                = direction == SignalDirection.Buy ? "Buy" : "Sell",
                type                = "Market",
                quantity            = quantity,
                limit_price         = (decimal?)null,
                stop_price          = (decimal?)null,
                client_order_id     = clientOrderId,
                requesting_module_id = ModuleId,
            };

            var envelope = EnvelopePayload.Create(
                MessageTypes.OrderCreate,
                ModuleId,
                payload);

            await _sender.SendEnvelopeAsync(envelope, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "OrderManager: ORDER_CREATE dispatched for {ClientOrderId} {Direction} {Qty} {Symbol}",
                clientOrderId, direction, quantity, symbol);
        }
        else
        {
            _logger.LogInformation(
                "OrderManager: paper trade filled immediately {ClientOrderId} {Direction} {Qty} {Symbol}",
                clientOrderId, direction, quantity, symbol);
        }

        return order;
    }

    /// <inheritdoc/>
    public async Task CancelOrderAsync(string clientOrderId, CancellationToken ct)
    {
        if (_cache.TryGetValue(clientOrderId, out var current))
        {
            var updated = current with { State = TraderOrderState.Cancelled, UpdatedAt = DateTimeOffset.UtcNow };
            _cache[clientOrderId] = updated;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new TradeRepository(db);
        await repo.UpdateStateAsync(clientOrderId, nameof(TraderOrderState.Cancelled), ct).ConfigureAwait(false);

        if (current?.PaperTrading == false)
        {
            var payload = new
            {
                client_order_id      = clientOrderId,
                requesting_module_id = ModuleId,
            };

            var envelope = EnvelopePayload.Create(MessageTypes.OrderCancel, ModuleId, payload);
            await _sender.SendEnvelopeAsync(envelope, ct).ConfigureAwait(false);
        }

        _logger.LogInformation("OrderManager: order {ClientOrderId} cancelled", TraderUtils.SafeLog(clientOrderId));
    }

    /// <inheritdoc/>
    public async Task UpdateOrderStateAsync(string clientOrderId, TraderOrderState newState, CancellationToken ct)
    {
        if (_cache.TryGetValue(clientOrderId, out var current))
        {
            _cache[clientOrderId] = current with { State = newState, UpdatedAt = DateTimeOffset.UtcNow };
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new TradeRepository(db);
        var updated = await repo.UpdateStateAsync(clientOrderId, newState.ToString(), ct).ConfigureAwait(false);

        if (!updated)
            _logger.LogWarning("OrderManager: no row found to update for {ClientOrderId}", clientOrderId);
    }

    /// <inheritdoc/>
    public async Task<TraderOrder?> GetOrderAsync(string clientOrderId, CancellationToken ct)
    {
        if (_cache.TryGetValue(clientOrderId, out var cached))
            return cached;

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo   = new TradeRepository(db);
        var entity = await repo.FindAsync(clientOrderId, ct).ConfigureAwait(false);

        if (entity is null) return null;

        var order = MapFromEntity(entity);
        _cache[clientOrderId] = order;
        return order;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TraderOrder> GetOpenOrdersAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var repo = new TradeRepository(db);

        await foreach (var entity in repo.GetOpenOrdersAsync(ct).ConfigureAwait(false))
            yield return MapFromEntity(entity);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static TradeEntity MapToEntity(TraderOrder o) => new()
    {
        ClientOrderId  = o.ClientOrderId,
        Symbol         = o.Symbol,
        Direction      = o.Direction.ToString(),
        Quantity       = o.Quantity,
        EntryPrice     = o.EntryPrice,
        StopLossPrice  = o.StopLossPrice,
        TakeProfitPrice = o.TakeProfitPrice,
        State          = o.State.ToString(),
        PaperTrading   = o.PaperTrading,
        CreatedAt      = o.CreatedAt,
        UpdatedAt      = o.UpdatedAt,
    };

    private static TraderOrder MapFromEntity(TradeEntity e) => new(
        ClientOrderId:  e.ClientOrderId,
        Symbol:         e.Symbol,
        Direction:      Enum.TryParse<SignalDirection>(e.Direction, out var d) ? d : SignalDirection.Hold,
        Quantity:       e.Quantity,
        EntryPrice:     e.EntryPrice,
        StopLossPrice:  e.StopLossPrice,
        TakeProfitPrice: e.TakeProfitPrice,
        State:          Enum.TryParse<TraderOrderState>(e.State, out var s) ? s : TraderOrderState.Draft,
        PaperTrading:   e.PaperTrading,
        CreatedAt:      e.CreatedAt,
        UpdatedAt:      e.UpdatedAt);
}
