---
mode: agent
description: "BCG Session 13 — Streaming Fabric: WebSockets, gRPC Streams, Webhooks, and API Inference"
status: "⏳ Pending — streaming transport governance not formalized"
depends-on: ["session-04", "session-06", "session-11"]
produces: ["docs/bcg/session-13-*.md", "src/core/MLS.Core/Streaming/"]
---

# Session 13 — Streaming Fabric: WebSockets, gRPC Streams, Webhooks, and API Inference

> **Status**: ⏳ Pending — WebSocket/SignalR exists but lacks formal QoS policy, dedupe, fan-out governance, and streaming class selection rules.

## Session Goal

Stabilize all real-time and near-real-time transport paths under one governed streaming fabric with declared QoS, retry/resume semantics, backpressure, fan-out rules, and transport selection law.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-13-extended-document.md` (source: `.prompts-update/BCG_Session_13_Extended_Document.md`)
- [ ] `streaming-transport-matrix.md` — WebSocket/SignalR vs gRPC streams vs webhook vs REST; decision table by workload
- [ ] `stream-qos-policy.md` — delivery guarantees per stream type, rate limits, buffer depth
- [ ] `stream-resume-replay-model.md` — client reconnect, sequence ID resume, NATS/Redis replay fallback
- [ ] `stream-fan-out-subscription-rules.md` — topic subscription, broadcast group limits, backpressure signals

### C# Streaming Abstractions (`src/core/MLS.Core/Streaming/`)
- [ ] `IStreamPublisher.cs` — `PublishAsync(StreamEvent)`, `CompleteAsync()`
- [ ] `IStreamSubscriber.cs` — `SubscribeAsync(topic, handler)`, `UnsubscribeAsync(topic)`
- [ ] `StreamEvent.cs` — record: sequenceId, topic, payload, timestamp, traceId, isFinal
- [ ] `StreamQosPolicy.cs` — record: deliveryGuarantee, maxRate, bufferDepth, retryCount, resumable
- [ ] `DeliveryGuarantee.cs` — enum: AtMostOnce, AtLeastOnce, OrderedPerTopic, BestEffortBroadcast
- [ ] `TopicSubscriptionManager.cs` — wraps Block Controller SignalR group subscription; thread-safe fan-out
- [ ] `WebhookIngestionService.cs` — HTTP POST intake, validates envelope structure, enriches before routing
- [ ] `BackpressureSignal.cs` — record: topicName, queueDepth, dropRate, producerShouldThrottle
- [ ] Add `STREAM_STARTED`, `STREAM_COMPLETED`, `STREAM_DROPPED`, `STREAM_RESUMED`, `BACKPRESSURE_SIGNAL` to `MessageTypes`

### Webhook Ingestion (`src/block-controller/`)
- [ ] Add `WebhookController.cs` — `POST /api/webhooks/{topic}` — validates, enriches envelope, routes internally
- [ ] Reject malformed payloads with 400; emit `WEBHOOK_INGESTION_FAILED` event
- [ ] Add rate-limiting middleware for webhook ingress

### Block Controller: Topic Subscription
- [ ] Align `SubscribeToTopicAsync` / `UnsubscribeFromTopicAsync` with `TopicSubscriptionManager`
- [ ] `IMessageRouter.BroadcastAsync` must use `Group("broadcast")` — confirm existing implementation

### Tests
- [ ] `TopicSubscriptionManagerTests.cs` — subscribe, receive, unsubscribe, fan-out to N clients
- [ ] `WebhookIngestionServiceTests.cs` — valid payload enriched and routed; malformed rejected
- [ ] `StreamQosPolicyTests.cs` — at-least-once dedupe token prevents double delivery
- [ ] `BackpressureSignalTests.cs` — signal emitted when queue depth exceeds threshold

## Skills to Apply

```
.skills/websockets-inferences.md     — SignalR hub groups, streaming inference, envelope schema
.skills/beast-development.md         — BoundedChannel, backpressure, MessagePack binary frames
.skills/dotnet-devs.md               — IAsyncEnumerable<T>, Channel<T>, rate limiting middleware
.skills/networking.md                — gRPC streams, HTTP/2, webhook ingress
.skills/system-architect.md          — transport selection matrix, QoS governance
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — all stream events via typed EnvelopePayload
- `IMessageRouter.BroadcastAsync` sends to `Group("broadcast")` — NOT `Clients.All`
- All `Channel<T>` with `BoundedChannelOptions` and explicit `FullMode` — no unbounded queues
- Streaming first event latency target: < 250 ms p95
- Webhook ingress: malformed payloads MUST fail fast — never silently swallowed

## Acceptance Gates

- [ ] `TopicSubscriptionManager` fan-out delivers to N concurrent subscribers without deadlock
- [ ] `WebhookController` rejects malformed payload with 400 within 50 ms
- [ ] `DeliveryGuarantee.AtLeastOnce` stream dedupe prevents double delivery with 10,000 messages
- [ ] All new tests pass: `dotnet test`
- [ ] 4 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Streaming/` | Create streaming abstractions here |
| `src/block-controller/MLS.BlockController/Hubs/BlockControllerHub.cs` | Existing SignalR hub |
| `src/block-controller/MLS.BlockController/Services/InMemoryMessageRouter.cs` | BroadcastAsync implementation |
| `src/core/MLS.Core/Contracts/EnvelopePayload.cs` | Envelope type for stream events |
| `.prompts-update/BCG_Session_13_Extended_Document.md` | Full session spec |
