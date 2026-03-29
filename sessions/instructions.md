# Session Instructions

## Starting a New Session

1. **Copy the template**:
   ```bash
   SESSION_ID="session-$(date +%Y%m%d-%H%M)"
   cp -r sessions/template/ sessions/$SESSION_ID/
   ```

2. **Fill in metadata** in `sessions/$SESSION_ID/SESSION.md`.

3. **Start infra**:
   ```bash
   make infra-up
   ```

4. **Open VS Code tasks** (`Ctrl+Shift+P → Tasks: Run Task`) and run:
   - `Dev: start all`

5. **Use the AI Prompts Sequence** from `sessions/ai-prompts.md` to drive Copilot.

---

## During a Session

- Keep `sessions/$SESSION_ID/SESSION.md` updated as you make decisions.
- Persist important context to the `memory` MCP server.
- Use `context7` MCP for library documentation.
- Use `github` MCP for issues and PR management.
- Follow all rules in `sessions/rules.md`.

---

## Closing a Session

1. **Verify** the module passes health checks:
   ```bash
   curl http://localhost:<PORT>/health
   ```

2. **Run tests**:
   ```bash
   make test-module MODULE=<name>
   ```

3. **Update session notes** in `SESSION.md` under "Results / Notes".

4. **Commit**:
   ```bash
   git add sessions/$SESSION_ID/
   git commit -m "docs(sessions): complete $SESSION_ID"
   ```

5. **Mark session status** as ✅ Complete in `SESSION.md`.

---

## Session File Index

Sessions are stored under `sessions/` with one folder per session:

```
sessions/
  template/        ← copy this to start a new session
    SESSION.md
  ai-prompts.md    ← master AI prompt sequence
  goals.md         ← project-level goals
  restrictions.md  ← hard restrictions
  configs.md       ← default config values
  rules.md         ← enforced rules
  instructions.md  ← this file
  session-YYYYMMDD-HHMM/  ← one folder per session
    SESSION.md
    notes.md        ← optional extra notes
```
