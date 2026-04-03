# broker — Session 3: Required Interfaces

> Use this document as context when generating Broker module code with GitHub Copilot.

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
