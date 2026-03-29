# Project Restrictions

These restrictions apply to ALL sessions and ALL contributors.

---

## 1. Security
- **No secrets committed.** Use `.env` (git-ignored) or environment variables.
- **No raw SQL string formatting.** All SQL must use parameterised queries.
- **No eval/exec on user input.** Sanitise all inputs at API boundary.

## 2. Architecture
- **No direct database access from modules.** All DB operations go through `src/storage/`.
- **No magic strings for routing.** Use enums from the enum registry.
- **No inter-module calls without Envelopes.** All messages must be typed payloads.
- **No bypassing the Invoker.** Modules do not call each other's internal functions directly.

## 3. Code Style
- **No functions longer than 50 lines** (excluding tests and doc-comments).
- **No file with more than one public function** (Function-as-File rule).
- **No untyped function signatures** in Python (use type annotations).
- **No `unwrap()`/`expect()` in production Rust code** without a comment.

## 4. Sessions
- **Do not edit `sessions/template/` directly.** Copy it to start a new session.
- **Sessions are only complete** when the module passes health checks and notes are committed.
- **Do not leave uncommitted work** at session close.

## 5. Dependencies
- **No new dependencies without justification** in session notes.
- **Prefer existing libraries** listed in project config files.
- **No version pinning to `*` or `latest`** in production configs.
