# Broker Module — Session Prompt

> Use this document as context when generating Broker module code with GitHub Copilot.

---

## 1. Module Identity

| Field | Value |
|---|---|
| **Name** | `broker` |
| **Namespace** | `MLS.Broker` |
| **Role** | HYPERLIQUID integration — order placement, fill tracking, position management |
| **HTTP Port** | `5800` |
| **WebSocket Port** | `6800` |
| **Container** | `mls-broker` |
| **Docker image** | `ghcr.io/somat3k/mls-broker:latest` |

---

## 2. Critical Rules

1. **HYPERLIQUID is the sole primary DEX/perpetuals broker** — no other venue hardcoded
2. All exchange API endpoints and contract addresses loaded from PostgreSQL `blockchain_addresses` — **never hardcoded**
3. Fallback chain: HYPERLIQUID → Broker1 → Broker2 (configured at runtime)
4. Orders must be idempotent — retried orders carry the same client order ID
5. **No Uniswap** — any reference should fail compilation via `.github/copilot-rules/rule-no-uniswap.md`

---

## 3. Required Interfaces

```csharp
namespace MLS.Broker.Interfaces;

/// <summary>HYPERLIQUID REST + WebSocket client abstraction.</summary>
public interface IHyperliquidClient
{
    Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct);
    Task<OrderResult> CancelOrderAsync(string clientOrderId, CancellationToken ct);
    Task<IReadOnlyList<OpenOrder>> GetOpenOrdersAsync(string symbol, CancellationToken ct);
    Task<Position?> GetPositionAsync(string symbol, CancellationToken ct);
    IAsyncEnumerable<FillNotification> SubscribeFillsAsync(CancellationToken ct);
    IAsyncEnumerable<OrderBookUpdate> SubscribeOrderBookAsync(string symbol, CancellationToken ct);
}

/// <summary>Cascading fallback chain across broker venues.</summary>
public interface IBrokerFallbackChain
{
    Task<OrderResult> ExecuteWithFallbackAsync(PlaceOrderRequest request, CancellationToken ct);
    Task<IReadOnlyList<string>> GetActiveBrokersAsync();
}

/// <summary>Tracks in-flight and completed orders with state persistence.</summary>
public interface IOrderTracker
{
    Task TrackAsync(OrderResult order, CancellationToken ct);
    Task UpdateAsync(string clientOrderId, OrderState newState, CancellationToken ct);
    Task<OrderResult?> GetAsync(string clientOrderId, CancellationToken ct);
    IAsyncEnumerable<OrderResult> GetOpenOrdersAsync(CancellationToken ct);
}
```

---

## 4. Key Payload Types Used

| Direction | Type | Description |
|---|---|---|
| Receives | `ORDER_CREATE` | From Trader or DeFi — place new order |
| Receives | `ORDER_CANCEL` | Cancel order by clientOrderId |
| Sends | `ORDER_CONFIRMATION` | Order accepted by venue |
| Sends | `FILL_NOTIFICATION` | Order partially or fully filled |
| Sends | `POSITION_UPDATE` | Current position state |
| Sends | `MODULE_HEARTBEAT` | To Block Controller every 5 s |

---

## 5. Data Models

```csharp
namespace MLS.Broker.Models;

public sealed record PlaceOrderRequest(
    string Symbol,
    OrderSide Side,
    OrderType Type,
    decimal Quantity,
    decimal? LimitPrice,
    string ClientOrderId,        // UUID — caller generated, idempotency key
    string RequestingModuleId
);

public sealed record OrderResult(
    string ClientOrderId,
    string? VenueOrderId,
    OrderState State,
    decimal FilledQuantity,
    decimal? AveragePrice,
    string Venue,
    DateTimeOffset CreatedAt
);

public enum OrderSide  { Buy, Sell }
public enum OrderType  { Market, Limit, StopMarket, StopLimit }
public enum OrderState { Pending, Open, PartiallyFilled, Filled, Cancelled, Rejected }
```

---

## 6. Database Dependencies

| Table | Purpose |
|---|---|
| `blockchain_addresses` | All HYPERLIQUID API endpoints and contract addresses |
| `orders` | Persistent order state (EF Core entity) |
| `positions` | Current open positions per symbol |

---

## 7. Skills to Apply

- `.skills/web3.md` — HYPERLIQUID REST/WS API, wallet, blockchain addresses
- `.skills/networking.md` — WebSocket subscription to exchange feeds, BC registration
- `.skills/websockets-inferences.md` — SignalR hub, envelope protocol
- `.skills/beast-development.md` — low-latency order routing, object pools
- `.skills/storage-data-management.md` — EF Core order persistence, Redis order cache
