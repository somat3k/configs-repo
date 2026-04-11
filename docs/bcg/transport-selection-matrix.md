# Transport Selection Matrix
## BCG Session 04 — Decision Table for Transport Class Assignment

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, envelope-law.md

---

## 1. Purpose

This document provides the decision table and selection rules for choosing the correct transport class and physical transport protocol for every inter-module message path in the BCG ecosystem.

---

## 2. Decision Table

| Scenario | Use | Class | Reason |
|----------|-----|-------|--------|
| Module registers with Block Controller on startup | gRPC / HTTP | A | Authoritative, typed, idempotency required |
| Block Controller acknowledges module registration | gRPC / HTTP response | A | Structured ack, version-checked |
| Module sends heartbeat | HTTP PATCH | A | Lightweight deterministic control-plane RPC |
| Block Controller routes a block execution command | gRPC | A | Authoritative, typed, causation-linked |
| Tensor transformation requested from one module | gRPC | A | Strict schema, lineage required |
| ML Runtime returns inference result | gRPC response | A | Deterministic, typed tensor reference |
| Batch execution starts or completes | gRPC | A | Structured, version-controlled |
| Model promotion (draft → production) | gRPC | A | State-changing, operator-confirmed |
| Operator dashboard receives live metric feed | SignalR / WebSocket | B | Live push, partial updates, UI-bound |
| Partial output streamed from running inference | SignalR / WebSocket | B | Incremental, best-effort stream |
| Topic subscriber receives routing update | SignalR / WebSocket | B | Fan-out, event-driven, tolerable drop |
| Runtime observability event emitted | SignalR / WebSocket | B | Live telemetry, non-authoritative |
| Shell VM streams PTY output to operator | SignalR / WebSocket | B | Continuous stream, session-scoped |
| External webhook received from partner system | HTTP/JSON | C | External ingress, normalization required |
| REST management endpoint called by operator tool | HTTP/JSON | C | Compatibility, human-initiated |
| Third-party data pushed into DataEvolution intake | HTTP/JSON | C | Normalization bridge, schema inference |
| Large tensor payload referenced in message | Artifact reference (Class D) | D | Payload too large for inline transport |
| Model artifact promoted from IPFS | Artifact reference (Class D) | D | Large binary, reference transport |
| Dataset snapshot referenced for TensorTrainer | Artifact reference (Class D) | D | Batch archive, integrity-verified |
| Replay package referenced for audit | Artifact reference (Class D) | D | Large, integrity-linked |

---

## 3. gRPC / Protobuf Selection Rules

**Use gRPC when**:
- the path is authoritative and correctness matters more than loose coupling
- the receiving module must explicitly acknowledge the message
- the payload schema is well-defined and machine-governed
- the Block Controller governs the route or state transition
- tensor execution or transformation semantics are involved
- typed errors and status codes improve the consumer experience

**Do not use gRPC when**:
- the path is broadcast-heavy operator telemetry with weak coupling
- the consumer is a browser that cannot use native gRPC without a proxy
- the interaction is a best-effort live stream where drops are acceptable

---

## 4. WebSocket / SignalR Selection Rules

**Use WebSocket / SignalR when**:
- live subscriptions are required with server-push semantics
- partial outputs must be pushed continuously to one or many consumers
- operator dashboards need real-time updates
- session-driven runtime interactions occur (shell, canvas, AI streaming)
- event fan-out to multiple consumers is required

**Do not use WebSocket / SignalR when**:
- the path requires strict unary RPC semantics with typed acknowledgement
- the message is a state-changing command requiring durability and retry logic
- schema version negotiation needs to be performed before delivery

---

## 5. HTTP / Webhook Selection Rules

**Use HTTP when**:
- integrating external systems that cannot use gRPC or WebSocket
- receiving external triggers via webhook
- serving management endpoints for operator tooling
- supporting controlled synchronous APIs with request/response semantics

**Do not use HTTP when**:
- the path is a high-frequency internal runtime lane (use gRPC)
- the path requires live streaming (use WebSocket / SignalR)
- the path is hot-path performance-critical (use gRPC + binary serialization)

---

## 6. Artifact Reference Selection Rules

**Use artifact reference transport when**:
- the payload body exceeds the inline size threshold (configurable, see tensor-storage-threshold.md)
- multiple consumers may need the same large object independently
- replay or archival is required for the payload
- the payload is a model artifact, dataset snapshot, or batch archive

**Do not use artifact reference when**:
- the payload is small enough for inline transport without performance impact
- the consumer requires the full payload body synchronously in the same request cycle

---

## 7. Serialization Selection

| Path Type | Serialization |
|-----------|--------------|
| gRPC Class A lanes | Protobuf (binary) |
| WebSocket hot event lanes | MessagePack (binary) preferred |
| WebSocket operator UI lanes | JSON (human-readable) acceptable |
| HTTP Class C ingress | JSON |
| HTTP management APIs | JSON |
| Artifact body storage | Raw binary (no envelope wrapping) |

JSON must not be used in high-frequency internal hot paths where protobuf or MessagePack alternatives are available.

---

## 8. Transport Class Assignment Authority

The Block Controller is the governing authority for transport class assignment where routing passes through the controller. Modules may self-declare their transport class for direct module-to-module paths, but the Block Controller may override or reject the assignment if it violates the transport governance spec.

Transport class mismatches detected at the Block Controller must emit `TRANSPORT_COMPATIBILITY_ERROR`.
