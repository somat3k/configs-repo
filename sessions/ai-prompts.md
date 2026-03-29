# AI Prompts — Master Sequence

This file defines the canonical sequence of AI (Copilot) prompts used to drive a session from zero to a working standalone module.

---

## Phase 0: Orientation

```
Load the file `.structure_pkg.json` and give me a summary of:
1. The project topology
2. Registered modules and their ports
3. Registered enums and their values
4. Which infrastructure services are active
```

```
Read the memory MCP server. Summarise any notes from previous sessions related to <MODULE>.
```

---

## Phase 1: Design

```
I want to build a module called <MODULE> that does <DESCRIPTION>.
Apply the Function-as-File rule and the Envelope protocol.
Propose the file structure.
```

```
List all commands, queries, and events this module will expose.
Map each to a MessageType enum value.
```

```
Show me the storage access pattern for <MODULE>.
Which backend(s) does it use? Generate the storage controller calls.
```

---

## Phase 2: Implementation

```
Scaffold the directory structure for <MODULE> at src/modules/<MODULE>/.
Create empty files for each function following Function-as-File.
```

```
Implement <FUNCTION> in its own file. Include:
- Full doc-comment (purpose, params, returns, errors)
- Typed input/output using the Envelope/Payload pattern
- Unit tests in a `#[cfg(test)]` block (Rust) or `test_` functions (Python)
```

```
Implement the HTTP server for <MODULE>:
- GET /health → {"status":"ok","module":"<MODULE>"}
- GET /info   → module metadata from .structure_pkg.json
- POST /invoke → accept Envelope, route to handler
```

---

## Phase 3: Integration

```
Implement the WebSocket client/server for <MODULE>.
It should:
- Connect to the service bus on startup
- Announce itself with a HealthCheckReply envelope
- Handle incoming Envelope messages by routing to the correct invoker
```

```
Generate the Docker container definition for <MODULE> in infra/docker-compose.yml.
Port: <PORT>. Network: devnet.
```

---

## Phase 4: Verification

```
Run `make test-module MODULE=<MODULE>` and show me the output.
Fix any failures.
```

```
Run `make lint` and fix all warnings.
```

```
Start the module with `make run MODULE=<MODULE>`.
Verify: curl http://localhost:<PORT>/health
```

---

## Phase 5: Session Close

```
Summarise everything implemented in this session as a bullet list.
List any decisions made and their rationale.
List any outstanding items.
Write this to sessions/<SESSION_ID>/SESSION.md under "Results / Notes".
```

```
Generate a conventional commit message for this session's changes.
Format: feat(<MODULE>): <summary>
```
