# configs-repo

> **Universal containerised, network-first mono-repo template.**
> Supports Rust · Python · Slint · Solidity (via Ape Framework) — backed by Redis, PostgreSQL & IPFS.

---

## Quick Start

```bash
# 1. Clone this template
git clone https://github.com/somat3k/configs-repo my-project
cd my-project

# 2. Copy env file and fill in values
cp .env.example .env

# 3. Start infrastructure (Redis, Postgres, IPFS)
make infra-up

# 4. Start a new development session
make new-session SESSION=session-01

# 5. Scaffold a new module
make new-module MODULE=auth
```

---

## Architecture

See [docs/architecture.md](docs/architecture.md) for the full diagram.

```
containers (devnet)
  ├── redis      :6379   — hot cache, pub/sub
  ├── postgres   :5432   — relational data
  └── ipfs       :5001   — content-addressed storage

modules (standalone services)
  ├── auth       :8001
  ├── storage    :8002
  └── compute    :8003

communication
  └── WebSocket + typed Envelope protocol
```

---

## Key Files

| File / Folder | Purpose |
|---|---|
| `.vscode/` | Shared VS Code settings, launch configs, tasks, MCP config |
| `.vscode/copilot-instructions.md` | Copilot behaviour rules and project context |
| `.vscode/mcp.json` | MCP server configuration (context7, memory, github, …) |
| `.structure_pkg.json` | Universal project manifest — modules, enums, rules |
| `sessions/` | Session templates, goals, rules, AI prompts |
| `sessions/template/` | **Copy this** to start a new session |
| `docs/` | Architecture, API reference, storage docs, graphs |
| `infra/` | Docker Compose for Redis, Postgres, IPFS |
| `Makefile` | All common development commands |
| `.env.example` | Environment variable documentation |
| `Cargo.toml` | Rust workspace root |
| `pyproject.toml` | Python project root |
| `slint.toml` | Slint UI configuration |
| `ape-config.yaml` | Solidity/Ape Framework configuration |
| `.github/copilot-rules/` | Enforced Copilot rules |
| `.github/workflows/` | CI, dynamic merge, issue review |

---

## Rules

| Rule | Description |
|---|---|
| `rule:function-as-file` | One public function per file |
| `rule:payload-envelope` | All messages use typed `Envelope` wrappers |
| `rule:hyperlink-enums` | All routing uses registered enum values |
| `rule:single-file-module` | Each module runs standalone with `/health` |
| `rule:session-complete` | Session done only when module passes all checks |

See [.github/copilot-rules/](.github/copilot-rules/) for full rule definitions.

---

## Sessions

Sessions are tracked in `sessions/`. Each session has:
- A `SESSION.md` with goals, restrictions, AI prompts, results.
- A canonical AI prompt sequence from `sessions/ai-prompts.md`.

```bash
# Start a new session
make new-session SESSION=session-auth-20240101

# Open VS Code task
# Ctrl+Shift+P → Tasks: Run Task → Session: new
```

---

## MCP Servers

Configured in `.vscode/mcp.json`:

| Server | Purpose |
|---|---|
| `context7` | Library documentation |
| `memory` | Persistent session memory |
| `github` | GitHub API |
| `linear` | Project management |
| `playwright` | Browser automation |
| `filesystem` | File access |
| `postgres` | Database queries |
| `redis` | Cache inspection |

---

## Contributing

1. Read [docs/review-guide.md](docs/review-guide.md).
2. Follow [Conventional Commits](https://www.conventionalcommits.org/).
3. Open a session issue using the [session template](.github/ISSUE_TEMPLATE/session.md).
4. Use the PR template for all pull requests.