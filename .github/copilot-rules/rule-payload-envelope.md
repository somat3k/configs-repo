# Rule: Payload Envelope

## Purpose
All inter-service and inter-module communication must use typed `Envelope` wrappers. This ensures every message is traceable, versioned, and validated.

## Enforcement

### Required fields
Every message must contain:
- `type` — a `MessageType` enum value (string)
- `version` — integer ≥ 1
- `session_id` — UUID string
- `payload` — typed object (never a raw dict)

### What to flag
- Passing raw dicts/maps as messages.
- Using `Any` as payload type in production code.
- Missing `session_id` in messages.
- Messages that are not validated before processing.

### What to require
- Define a concrete `Payload` struct/class for every message type.
- Validate envelope on receipt (schema validation or typed deserialization).
- Return an `Envelope` with `type=Error` on validation failure.

## Example

❌ Bad:
```python
await ws.send(json.dumps({"action": "put", "data": bytes}))
```

✅ Good:
```python
await ws.send(Envelope(
    type=MessageType.COMMAND,
    version=1,
    session_id=session_id,
    payload=StoragePutPayload(data=data),
).json())
```
