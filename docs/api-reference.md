# API Reference

## Envelope Protocol

Every message exchanged between modules must conform to the `Envelope` schema.

### Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Envelope",
  "type": "object",
  "required": ["type", "version", "session_id", "payload"],
  "properties": {
    "type":       { "type": "string", "description": "MessageType enum value" },
    "version":    { "type": "integer", "minimum": 1 },
    "session_id": { "type": "string", "format": "uuid" },
    "payload":    { "type": "object" }
  }
}
```

### MessageType Enum Values

| Value              | Direction        | Description                       |
|--------------------|------------------|-----------------------------------|
| `Command`          | Client → Module  | Trigger a state-changing action   |
| `Query`            | Client → Module  | Request read-only data            |
| `Event`            | Module → *       | Broadcast a state change          |
| `Error`            | Any              | Error response                    |
| `HealthCheck`      | Mesh → Module    | Liveness probe                    |
| `HealthCheckReply` | Module → Mesh    | Health status reply                |

---

## Module HTTP API Conventions

### Required endpoints

| Method | Path      | Description              |
|--------|-----------|--------------------------|
| GET    | `/health` | Liveness check → `{"status":"ok"}` |
| GET    | `/info`   | Module metadata          |
| POST   | `/invoke` | Accept an `Envelope`     |

### WebSocket

- Connect at `ws://<host>:<port>/ws`
- All frames are UTF-8 JSON `Envelope` objects.
- Heartbeat: server sends `{"type":"ping"}` every 30 s; client must reply `{"type":"pong"}`.

---

## Command / Query Invoker Pattern

```
Invoker
 └── resolves target module from enum registry
 └── wraps args in Envelope
 └── sends via WebSocket or HTTP POST /invoke
 └── awaits response Envelope
 └── unwraps and returns typed result
```

### Example (Rust pseudo-code)

```rust
let result = Invoker::new()
    .target(Module::Storage)
    .command(StorageCommand::Put { data: bytes })
    .invoke()
    .await?;
```

### Example (Python pseudo-code)

```python
result = await invoker.send(
    target=Module.STORAGE,
    envelope=Envelope(
        type=MessageType.COMMAND,
        payload=StoragePutPayload(data=data),
    )
)
```
