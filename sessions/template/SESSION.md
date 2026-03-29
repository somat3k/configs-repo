# Session: {{SESSION_ID}}

> **Copy this file** to `sessions/{{SESSION_ID}}/` and fill in the sections below.
> Do NOT edit `sessions/template/` directly.

---

## Metadata
| Field        | Value              |
|--------------|--------------------|
| Session ID   | `{{SESSION_ID}}`   |
| Date         | `{{DATE}}`         |
| Author       | `{{AUTHOR}}`       |
| Module focus | `{{MODULE}}`       |
| Status       | 🔴 In Progress     |

---

## Goals
> What must be true at the end of this session?

- [ ] Goal 1
- [ ] Goal 2
- [ ] Goal 3

---

## Restrictions
> What must NOT happen in this session?

- Do not modify `sessions/template/` directly.
- Do not commit secrets.
- Do not bypass the Envelope protocol.
- Do not access databases directly from module logic.

---

## AI Prompts Sequence
> Ordered list of prompts to drive Copilot through this session.

1. **Context load** — "Load `.structure_pkg.json` and summarise the project topology."
2. **Memory recall** — "Read session memory for module `{{MODULE}}`."
3. **Goal clarify** — "I want to implement `<feature>`. Walk me through the design using the Function-as-File rule."
4. **Scaffold** — "Scaffold the file structure for this feature."
5. **Implement** — "Implement `<function>` in its own file with full doc-comments and unit tests."
6. **Verify** — "Run `make test` and `make lint`. Show me any failures."
7. **Health check** — "Confirm `GET /health` returns 200 for the module."
8. **Mesh register** — "Register the module on the service bus."
9. **Commit notes** — "Summarise what was implemented and save to session notes."

---

## Configs
> Session-specific configuration overrides (do not commit secrets).

```env
# .env overrides for this session
MODULE_PORT=8001
LOG_LEVEL=debug
```

---

## Rules
> Rules enforced in this session (reference canonical rules).

- `rule:hyperlink-enums` — all cross-references use enum paths.
- `rule:payload-envelope` — all messages use typed `Envelope`.
- `rule:function-as-file` — one public function per file.
- `rule:single-file-module` — module runs standalone.
- `rule:session-complete` — session ends with working module + notes committed.

---

## Instructions
> Step-by-step instructions for this session's work.

1. Start infra: `make infra-up`
2. Create module scaffold: `make new-module NAME={{MODULE}}`
3. Implement features following Function-as-File rule.
4. Write unit tests alongside each function.
5. Run `make test-module MODULE={{MODULE}}`.
6. Verify health endpoint: `curl http://localhost:{{PORT}}/health`.
7. Register on mesh.
8. Commit: `feat({{MODULE}}): <summary>`.
9. Update this session file with results.

---

## Results / Notes
> Fill in at end of session.

### What was built


### Decisions made


### Outstanding items
- [ ] Item 1

### Commit references
- `abc1234` — initial scaffold
