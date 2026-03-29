# Copilot Instructions

## Project Overview
This is a **containerised, network-first** mono-repo template. It supports Rust, Python, Slint, and Solidity services backed by Redis, PostgreSQL and IPFS running in Docker containers. All inter-module communication goes through WebSocket APIs or REST inferences.

---

## Architecture Principles

### 1. Function-as-File (Golden Rule)
> Every public function lives in its own file. All related helpers, types and invocations orbit that file.

```
src/
  commands/          ← command handlers (one file = one command)
  invokers/          ← invoker wrappers (one file = one invoker)
  queries/           ← query functions  (one file = one query)
  modules/           ← self-contained module entry points
```

### 2. Payload-Specific Communication
All inter-service messages **must** use typed payload envelopes:
```json
{ "type": "ENUM_KEY", "version": 1, "session_id": "uuid", "payload": { ... } }
```
- `type` must reference a registered `MessageType` enum value.
- Never use raw strings for message routing.

### 3. Hyperlink & Enum Rule
- Every cross-module reference uses a fully-qualified enum path (e.g. `Module::Action::Variant`).
- No magic strings, no raw URL literals in logic code; define constants in `src/constants/`.

### 4. Single-File Modules
Each module must compile/run as a standalone binary or service and expose a health endpoint at `GET /health`.

### 5. Session Completion Rule
A session is only complete when:
- [ ] The module runs standalone (`cargo run --bin <name>`).
- [ ] It passes health checks.
- [ ] It registers itself on the service mesh.
- [ ] Session notes are committed to `sessions/<session-id>/`.

---

## Copilot Behaviour Settings

### Agent Mode
- Think step-by-step before proposing changes.
- Prefer existing abstractions; propose new ones only when justified.
- Use MCP tools (`context7`, `memory`, `github`) to enrich context before answering.

### Code Generation
- Rust: `#[derive(Debug, Clone, Serialize, Deserialize)]` on all data structs.
- Python: type-annotate all public functions; use `pydantic` models for payloads.
- Solidity: target `^0.8.20`; use `SPDX-License-Identifier` header.
- Always add doc-comments (`///` Rust, `"""` Python, `/** */` Solidity).

### Context & Memory
- Persist important discoveries in the `memory` MCP server.
- Load `.structure_pkg.json` at session start to understand project topology.
- Use `context7` to look up library docs before writing library-dependent code.

### Review & Testing
- Suggest tests alongside every new function.
- Tag TODOs with `// TODO(<session-id>): description`.
- Run `ruff check . && cargo clippy --all-targets -- -D warnings` (lint) and `cargo test --bin <name>` (test) before marking a task done.

---

## Infra Shortcuts
| Resource   | Default URL                          |
|------------|--------------------------------------|
| Redis      | `redis://localhost:6379`             |
| Postgres   | `postgresql://postgres:postgres@localhost:5432/devdb` |
| IPFS API   | `http://localhost:5001`              |
| IPFS GW    | `http://localhost:8080`              |

---

## Skills & Tools
Use these MCP servers when relevant:
- **context7** — look up library/framework docs
- **memory** — read/write session memory
- **github** — manage issues, PRs, and workflow runs
- **linear** — sync tasks with Linear issues
- **playwright** — browser automation and UI verification
- **filesystem** — read/write workspace files
- **postgres** / **redis** — inspect live data

---

## Enum Registry Pattern
Define all project-wide enums in `.structure_pkg.json → enums[]`. Reference them by canonical path. Example:

```
StorageBackend::IPFS
StorageBackend::Postgres
MessageType::Command
MessageType::Query
MessageType::Event
```
