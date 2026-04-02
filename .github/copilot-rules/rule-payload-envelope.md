---
rule: payload-envelope
applies-to: "**/*.cs"
---

# Payload Envelope Protocol

All inter-module WebSocket messages MUST use the Envelope Protocol.

## Required Fields

```csharp
public record EnvelopePayload(
    string Type,           // MessageTypes constant — never a raw string literal
    int Version,           // Schema version, must be >= 1
    Guid SessionId,        // UUID v4 for request correlation
    string ModuleId,       // Sender's module_id from registry
    DateTimeOffset Timestamp, // UTC timestamp of creation
    JsonElement Payload    // Strongly-typed payload serialized to JsonElement
);
```

## Rules

1. **`Version` must be >= 1** — never send version 0
2. **`Type` must reference `MessageTypes.*` constant** — no raw strings
3. **`SessionId` must be a new UUID per message** — use `Guid.NewGuid()`
4. **`ModuleId` must match the module's registered ID** — load from `IModuleIdentity`
5. **`Payload` must be a non-null JSON object** — use strongly-typed payload records

## Example

```csharp
var envelope = new EnvelopePayload(
    Type: MessageTypes.TradeSignal,
    Version: 1,
    SessionId: Guid.NewGuid(),
    ModuleId: _moduleIdentity.ModuleId,
    Timestamp: DateTimeOffset.UtcNow,
    Payload: JsonSerializer.SerializeToElement(new TradeSignalPayload(
        Symbol: "BTC-PERP",
        Side: TradeSide.Buy,
        Price: 42000.50m,
        Confidence: 0.87f
    ))
);
```

## References
- [docs/payload-schemas.md](../../docs/payload-schemas.md)
- [.skills/websockets-inferences.md](../../.skills/websockets-inferences.md)
- [.skills/system-architect.md](../../.skills/system-architect.md)
