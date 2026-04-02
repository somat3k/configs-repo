---
name: websockets-inferences
source: custom (MLS Trading Platform)
description: 'WebSocket server/client patterns, real-time data streaming, SignalR hubs, inference API design, and payload schema management for the MLS inter-module network.'
---

# WebSockets & Inference — MLS Trading Platform

## WebSocket Architecture
Every MLS module implements a dual WebSocket role:
1. **Server**: Accepts connections from Web App and other modules
2. **Client**: Connects to BlockController and Data Layer for subscriptions

## SignalR Hub Pattern (C# Server)
```csharp
public class ModuleHub : Hub<IModuleHubClient>
{
    public async Task Subscribe(string[] topics) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, string.Join(",", topics));

    public async Task SendPayload(EnvelopePayload envelope) =>
        await _moduleService.ProcessAsync(envelope, Context.ConnectionAborted);
}

public interface IModuleHubClient
{
    Task ReceiveEnvelope(EnvelopePayload envelope);
    Task ReceiveHeartbeat(HeartbeatPayload heartbeat);
    Task ReceiveInferenceResult(InferenceResult result);
}
```

## Inference API Design
Each module exposes inference endpoints:
- `POST /api/inference/run` — synchronous inference (< 100ms)
- `GET /api/inference/stream` — SSE streaming inference
- `WS /ws/inference` — bidirectional WebSocket inference
- `POST /api/inference/batch` — batch inference (async, returns job ID)

## Payload Schemas
All WebSocket messages conform to the Envelope Protocol:
```csharp
public record EnvelopePayload(
    string Type,
    int Version,
    Guid SessionId,
    string ModuleId,
    DateTimeOffset Timestamp,
    JsonElement Payload
);

// Example typed payloads:
public record TradeSignalPayload(string Symbol, decimal Price, TradeSide Side, decimal Confidence);
public record ArbitrageOpportunityPayload(string BuyExchange, string SellExchange, decimal Spread, decimal Volume);
public record HeartbeatPayload(string ModuleId, ModuleStatus Status, SystemMetrics Metrics);
```

## Message Type Constants
```csharp
public static class MessageTypes
{
    public const string ModuleRegister = "MODULE_REGISTER";
    public const string ModuleHeartbeat = "MODULE_HEARTBEAT";
    public const string TradeSignal = "TRADE_SIGNAL";
    public const string ArbitrageOpportunity = "ARBITRAGE_OPPORTUNITY";
    public const string InferenceRequest = "INFERENCE_REQUEST";
    public const string InferenceResult = "INFERENCE_RESULT";
    public const string DataUpdate = "DATA_UPDATE";
    public const string SystemEvent = "SYSTEM_EVENT";
}
```

## Real-Time Data Streaming
- Use `IAsyncEnumerable<T>` + SignalR streaming for market data
- Buffer updates with `Channel<T>` for backpressure management
- Apply rate limiting per connection: 1000 messages/second max
- Use binary MessagePack encoding for high-frequency data
- Fallback to JSON for cross-language compatibility
