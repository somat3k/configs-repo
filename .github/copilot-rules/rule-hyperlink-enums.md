# Rule: Hyperlink Enums

## Purpose
Prevent magic strings and free-form URL literals in logic code. All cross-module references and routing keys must use registered enum values.

## Enforcement

### What to flag
- String literals used as routing keys, message types, or module references.
- Hardcoded URL strings in business logic (not in config).
- `match` / `switch` on raw strings instead of enums.

### What to require
- All message type values must come from `MessageType` enum.
- All module identifiers must come from `Module` enum.
- All storage backend references must come from `StorageBackend` enum.
- All hyperlinks in docs must reference the canonical enum path.

## Example

❌ Bad:
```rust
if msg_type == "command" { ... }
let url = "ws://localhost:9000/bus";
```

✅ Good:
```rust
if msg_type == MessageType::Command { ... }
let url = Config::bus_url(); // from config, not hardcoded
```

## Enum Registry
All enums are defined in `.structure_pkg.json → enums[]`.
