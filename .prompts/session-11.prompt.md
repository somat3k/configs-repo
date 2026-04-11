---
mode: agent
description: "BCG Session 11 — Live Runtime, Hot Refresh, and Session Joinability"
status: "⏳ Pending — hot refresh protocol and session joinability not implemented"
depends-on: ["session-02", "session-04", "session-10"]
produces: ["docs/bcg/session-11-*.md", "src/block-controller/", "src/modules/shell-vm/"]
---

# Session 11 — Live Runtime, Hot Refresh, and Session Joinability

> **Status**: ⏳ Pending — rolling update, drain-and-replace, and live session attachment are not formally implemented.

## Session Goal

Allow the BCG fabric to remain alive during module updates, permit operators to join live update sessions safely, and define emergency rollback paths.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-11-extended-document.md` (source: `.prompts-update/BCG_Session_11_Extended_Document.md`)
- [ ] `live-update-protocol.md` — rolling update sequence: drain → swap → resume → verify
- [ ] `runtime-session-model.md` — session join/leave, session state snapshot, re-attachment
- [ ] `safe-drain-procedure.md` — drain request → in-flight quiesce → handoff → verify empty → offline
- [ ] `shell-privilege-policy.md` — which commands allowed in ShellVM during live update, privilege escalation rules
- [ ] `hot-refresh-qa-checklist.md` — pre/during/post update test gates

### Block Controller: Drain and Hot-Replace (`src/block-controller/`)
- [ ] Add `DrainService.cs` — sends `DRAIN_REQUESTED` to target module, waits for `DRAIN_COMPLETE` ACK
- [ ] Add `HotReplaceOrchestrator.cs` — drain → wait → signal new instance → verify registration → resume routing
- [ ] Add `LiveSessionRegistry.cs` — tracks connected operator sessions with last-seen and re-attachment tokens
- [ ] Expose `POST /api/modules/{moduleId}/drain` — initiates drain workflow
- [ ] Expose `GET /api/sessions` — list active operator sessions
- [ ] Emit `DRAIN_REQUESTED`, `DRAIN_COMPLETE`, `MODULE_HOT_REPLACED`, `SESSION_REATTACHED` events

### Module: Drain Handler (all modules)
- [ ] Add `IDrainHandler.cs` to `src/core/MLS.Core/` — `DrainAsync(CancellationToken): Task`
- [ ] Implement `DrainHandler` in Block Controller, Trader, ML Runtime, ShellVM — quiesce in-flight work, emit `DRAIN_COMPLETE`
- [ ] ML Runtime hot-reload: swap-first + 500 ms delayed dispose pattern (already partially implemented)

### ShellVM: Session Joinability (`src/modules/shell-vm/`)
- [ ] Add `SessionJoinService.cs` — validates re-attachment token, restores output stream subscription
- [ ] Add `GET /api/sessions/{sessionId}/attach` — returns WebSocket URL with token

### Tests
- [ ] `DrainServiceTests.cs` — drain completes when module ACKs within timeout
- [ ] `HotReplaceOrchestratorTests.cs` — drain → replace → route restored end-to-end
- [ ] `SessionReattachmentTests.cs` — operator can rejoin a live session after disconnect

## Skills to Apply

```
.skills/websockets-inferences.md     — SignalR group management, hub reconnection
.skills/networking.md                — drain protocol, TCP keep-alive, endpoint refresh
.skills/dotnet-devs.md               — CancellationToken, Channel<T>, IHostedService
.skills/system-architect.md          — live update patterns, session governance
.skills/beast-development.md         — graceful shutdown, GracefulShutdownTimeout
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — drain and session events via typed EnvelopePayload
- `IMessageRouter.BroadcastAsync` sends to `Group("broadcast")` — NOT `Clients.All`
- Module drain MUST quiesce all in-flight `Channel<T>` consumers before emitting `DRAIN_COMPLETE`
- Hot refresh must NOT cause total fabric collapse — at least one routing path must remain active

## Acceptance Gates

- [ ] `POST /api/modules/{moduleId}/drain` triggers drain and module emits `DRAIN_COMPLETE` within 10 s
- [ ] `HotReplaceOrchestrator` routes to replacement module within 30 s of drain completion
- [ ] ShellVM session survives a module restart with re-attachment
- [ ] All new tests pass: `dotnet test`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/block-controller/MLS.BlockController/Services/` | Add DrainService, HotReplaceOrchestrator |
| `src/core/MLS.Core/` | Add IDrainHandler interface |
| `src/modules/shell-vm/MLS.ShellVM/Services/` | SessionJoinService |
| `src/modules/ml-runtime/MLS.MLRuntime/Models/ModelRegistry.cs` | Hot-reload swap pattern (reference) |
| `.prompts-update/BCG_Session_11_Extended_Document.md` | Full session spec |
