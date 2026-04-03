# block-controller — Session 3: Standalone Network Node

> Use this document as context when generating Block Controller code with GitHub Copilot.

---

## Overview

After Session 2, the Block Controller is a fully operational strategy routing engine.
Session 3 upgrades it to a **standalone network node** — any WebSocket-capable client
(module, user tool, dashboard, external system) can connect and participate in the
bidirectional envelope exchange without requiring a pre-arranged module identity.

---

## Bidirectional Value-Exchange Protocol

The core design principle is symmetric peer communication:

```
External Client                         Block Controller Hub
     │                                           │
     │  Connect: ws://host:6100/hubs/block-controller          │
     │  ?moduleId=<guid>  or  ?clientId=<guid>   │
     ├──────────────────────────────────────────►│  OnConnectedAsync()
     │                                           │  → AddToGroup("broadcast")
     │                                           │  → AddToGroup(<peerId>)
     │                                           │
     │  SendEnvelope(envelope)                   │
     │  [CLIENT OUTPUT → HUB INPUT]              │
     ├──────────────────────────────────────────►│  Routes/deploys based on Type
     │                                           │
     │                ReceiveEnvelope(envelope)  │
     │  [HUB OUTPUT → CLIENT INPUT]              │
     │◄──────────────────────────────────────────┤  Targeted or broadcast delivery
     │                                           │
     │  SubscribeToTopicAsync(topic)             │
     ├──────────────────────────────────────────►│  AddToGroup(topic)
     │                                           │  SubscriptionTable.AddAsync(topic, id)
     │                  ReceiveEnvelope(routed)  │
     │◄──────────────────────────────────────────┤  When envelope with matching type arrives
     │                                           │
     │  UnsubscribeFromTopicAsync(topic)         │
     ├──────────────────────────────────────────►│  RemoveFromGroup(topic)
     │                                           │
```

**Inputs become outputs and outputs become inputs** — the same WebSocket connection is
simultaneously the channel for sending envelopes TO the hub AND receiving routed envelopes
FROM the hub.

---

## Connection Guide

### Module Connection (server-to-server)

```javascript
// JavaScript/TypeScript SignalR client
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://block-controller:6100/hubs/block-controller?moduleId=<yourModuleId>")
    .withAutomaticReconnect()
    .build();

connection.on("ReceiveEnvelope", (envelope) => {
    console.log("Received:", envelope.type, envelope.payload);
    // Process envelope — this is the "client input" side
});

await connection.start();

// Send an envelope — this is the "client output" side
await connection.invoke("SendEnvelope", {
    type: "TRADE_SIGNAL",
    version: 1,
    session_id: crypto.randomUUID(),
    module_id: "<yourModuleId>",
    timestamp: new Date().toISOString(),
    payload: { symbol: "BTC-PERP", side: "BUY", confidence: 0.87 }
});
```

### C# Module Connection

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl($"http://block-controller:6100/hubs/block-controller?moduleId={moduleId}")
    .WithAutomaticReconnect()
    .Build();

connection.On<EnvelopePayload>("ReceiveEnvelope", async envelope =>
{
    await _envelopeHandler.HandleAsync(envelope, ct);
});

await connection.StartAsync(ct);
```

### Anonymous External Client (tools, dashboards)

```javascript
// Omit moduleId — receives broadcast envelopes only
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://block-controller:6100/hubs/block-controller?clientId=" + clientId)
    .build();

// Subscribe to a specific topic after connecting
await connection.invoke("SubscribeToTopicAsync", "BLOCK_SIGNAL");
```

---

## Dynamic Topic Subscription

After connecting, any client can subscribe to arbitrary topics at runtime:

```csharp
// Subscribe — adds connection to topic group AND registers in SubscriptionTable
await hubConnection.InvokeAsync("SubscribeToTopicAsync", "BLOCK_SIGNAL");

// Later, unsubscribe
await hubConnection.InvokeAsync("UnsubscribeFromTopicAsync", "BLOCK_SIGNAL");
```

Topic names match `StrategyRouter.BuildTopic(strategyId, blockId, socketName)` format
for block-level signal routing, or any `MessageTypes.*` constant for module-level routing.

---

## Group Model

| Group Name | Members | Purpose |
|------------|---------|---------|
| `broadcast` | ALL connections | Platform-wide envelopes (STRATEGY_STATE_CHANGE, health) |
| `<moduleId>` / `<clientId>` | Single peer | Targeted delivery to a specific module or client |
| `<strategyId>/<blockId>/<socket>` | Block subscribers | BLOCK_SIGNAL routing for deployed strategies |
| Any custom string | Opt-in via SubscribeToTopicAsync | Dynamic user-defined routing groups |

---

## Hub Methods Reference

| Method | Direction | Description |
|--------|-----------|-------------|
| `SendEnvelope(envelope)` | Client → Hub | Primary envelope ingress |
| `SubscribeToTopicAsync(topic)` | Client → Hub | Join a topic group |
| `UnsubscribeFromTopicAsync(topic)` | Client → Hub | Leave a topic group |
| `ReceiveEnvelope(envelope)` | Hub → Client | Targeted or broadcast delivery |

---

## Standalone Node Operation

The Block Controller runs as a fully independent, self-contained process:

```bash
# Run standalone (development)
cd src/block-controller/MLS.BlockController
dotnet run

# Ports:
#   HTTP API:             http://0.0.0.0:5100  (REST: /api/modules, /health)
#   WebSocket/SignalR:    http://0.0.0.0:6100  (hub: /hubs/block-controller)

# Docker
docker build -f src/block-controller/Dockerfile -t mls-block-controller src
docker run -p 5100:5100 -p 6100:6100 mls-block-controller

# Health check
curl http://localhost:5100/health
```

No other modules are required for the Block Controller to start and accept connections.
Modules that are not yet running simply mean their subscription groups are empty — envelopes
to those groups are silently dropped (no exceptions).

---

## Skills Applied in Session 3

- `.skills/dotnet-devs.md` — primary constructor DI, async patterns
- `.skills/websockets-inferences.md` — SignalR hub lifecycle, group management
- `.skills/networking.md` — standalone node, connection URL format
- `.skills/beast-development.md` — bounded Channel, DropOldest, lock-free groups
- `.skills/designer.md` — strategy routing, socket topic keys
