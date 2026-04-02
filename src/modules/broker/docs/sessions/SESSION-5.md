# broker — Session 5: Data Models

> Use this document as context when generating Broker module code with GitHub Copilot.

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
