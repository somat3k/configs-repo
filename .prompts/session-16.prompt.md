---
mode: agent
description: "BCG Session 16 — UI and Operator Experience"
status: "⏳ Pending — Blazor shell exists; operator plane for block composition, tensor flows, and live session control needed"
depends-on: ["session-11", "session-13", "session-15"]
produces: ["docs/bcg/session-16-*.md", "src/web-app/WebApp/Components/", "src/modules/designer/"]
---

# Session 16 — UI and Operator Experience

> **Status**: ⏳ Pending — Blazor MDI shell and Designer block graph exist; full operator plane (tensor visualization, live session control, runtime observatory, safe admin tools) not yet implemented.

## Session Goal

Advance the Blazor + Fluent UI surface into a premium operator plane where operators can compose block strategies, observe tensor flows, control live sessions, and perform production-safe administrative actions.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-16-extended-document.md` (source: `.prompts-update/BCG_Session_16_Extended_Document.md`)
- [ ] `ui-operating-model.md` — operator roles, screen flows, safety gates for destructive actions
- [ ] `graph-canvas-interaction-spec.md` — block drag/drop, socket connection rules, composition nesting UX
- [ ] `runtime-observatory-dashboards.md` — dashboard specs: module health, tensor flows, kernel execution, SLO gauges
- [ ] `admin-interaction-risk-policy.md` — confirmation dialogs, audit trail display, undo/rollback UI

### Blazor Designer Improvements (`src/modules/designer/`)
- [ ] Add live block execution status overlay — show kernel state (Idle/Running/Streaming/Faulted) on each block
- [ ] Add tensor flow visualization — animate edge when tensor is in transit between blocks
- [ ] Add `StrategyDeployPanel.razor` — deploy, pause, drain, and rollback a composed strategy
- [ ] Wire `StrategyDeployPanel` to Block Controller drain/hot-replace API (Session 11)
- [ ] Add block search and filter to block palette

### Blazor Web App: Runtime Observatory (`src/web-app/WebApp/`)
- [ ] Add `ObservatoryPage.razor` — real-time module health grid: status, heartbeat age, capability score, active kernels
- [ ] Add `TensorFlowPage.razor` — live tensor lineage viewer: origin module → transform chain → destination
- [ ] Add `KernelExecutionPage.razor` — kernel activity feed: running, completed, faulted events with latency
- [ ] Add `SloGaugePage.razor` — SLO gauge widgets bound to live observability metrics (Session 15)
- [ ] Connect all pages via SignalR to Block Controller broadcast group

### AI Hub: Canvas Actions (`src/modules/ai-hub/`)
- [ ] Confirm all canvas-producing plugin functions dispatch `CanvasAction` BEFORE returning string result
- [ ] Add `CanvasAction` types: `OpenObservatory`, `FocusBlock`, `HighlightTensorPath`
- [ ] Streaming responses use `kernel.InvokeStreamingAsync` — verify no buffered responses

### Admin Panel
- [ ] Add `AdminPage.razor` (Admin role only — guarded by RBAC from Session 14)
- [ ] Actions: drain module, promote model, issue module token, view audit log
- [ ] Every destructive action shows confirmation dialog + writes to audit log

### Tests
- [ ] Blazor component tests for `ObservatoryPage` — renders module list from SignalR update
- [ ] `StrategyDeployPanel` integration test — clicks Deploy, verifies drain API called
- [ ] `AdminPage` renders only for Admin role; throws 403 for Module role

## Skills to Apply

```
.skills/premium-uiux-blazor.md       — FluentUI Blazor, MDI canvas, SignalR updates, chart components
.skills/web-apps.md                  — Blazor component patterns, cascading auth state
.skills/ai-hub.md                    — CanvasActionDispatcher, SK plugin canvas actions
.skills/designer.md                  — block graph, socket types, ICompositionGraph
.skills/websockets-inferences.md     — SignalR client subscription, hub reconnect
.skills/artificial-intelligence.md   — Semantic Kernel streaming, InvokeStreamingAsync
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-designer-blocks.md` — all block types implement `IBlockElement`; socket connections typed
- Canvas actions MUST go through `CanvasActionDispatcher` — NEVER directly mutate UI state
- State-modifying actions (drain, deploy) MUST require confirmed bool parameter before execution
- Admin routes gated by RBAC from Session 14 — no anonymous access
- Streaming responses: `kernel.InvokeStreamingAsync` — NEVER buffer complete response

## Acceptance Gates

- [ ] `ObservatoryPage` updates in < 1 s when a module heartbeat changes state
- [ ] Block execution status overlay reflects live `KernelState` via SignalR
- [ ] `AdminPage` renders for Admin role; returns 403 for Module role
- [ ] Canvas action `FocusBlock` highlights correct block in graph without direct state mutation
- [ ] All new component tests pass: `dotnet test`
- [ ] 4 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/web-app/WebApp/Components/` | Blazor pages and components |
| `src/modules/designer/` | Designer block graph (extend) |
| `src/modules/ai-hub/` | AI Hub canvas actions |
| `src/block-controller/MLS.BlockController/Hubs/BlockControllerHub.cs` | SignalR broadcast source |
| `.github/copilot-rules/rule-designer-blocks.md` | Block graph rules |
| `.prompts-update/BCG_Session_16_Extended_Document.md` | Full session spec |
