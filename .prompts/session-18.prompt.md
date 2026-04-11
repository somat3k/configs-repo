---
mode: agent
description: "BCG Session 18 — Module Species Packs"
status: "⏳ Pending — no per-module 20-document packs exist beyond Session 01 templates"
depends-on: ["session-01", "session-09", "session-17"]
produces: ["docs/modules/{module}/"]
---

# Session 18 — Module Species Packs

> **Status**: ⏳ Pending — 20-document packs for each module not yet written. Only the template and doctrine exist from Session 01.

## Session Goal

Turn every module into a separate living species with its own complete 20-document governance pack, inter-species communication contracts, and evolution roadmap.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-18-extended-document.md` (source: `.prompts-update/BCG_Session_18_Extended_Document.md`)
- [ ] `species-pack-template.md` — expanded 20-document template with per-section guidance
- [ ] `cross-module-compatibility-guide.md` — which modules can exchange tensors directly; adapter requirements
- [ ] `evolution-path-matrix.md` — planned capability additions per module across sessions 19–20
- [ ] `module-ownership-stewardship-matrix.md` — owning team/agent, escalation path, change approval

### Per-Module Document Packs

For each module listed below, create `docs/modules/{module}/` with all 20 documents.  
Modules: **block-controller, web-app, designer, trader, arbitrager, defi, ml-runtime, data-layer, ai-hub, broker, transactions, shell-vm, data-evolution, tensor-trainer**

Each pack must include:
- [ ] `01-module-charter.md` — mission, species class, governance owner
- [ ] `02-context-scope.md` — what it does, what it does NOT do, upstream/downstream
- [ ] `03-architecture-overview.md` — component diagram, key services, DI graph
- [ ] `04-runtime-contract.md` — port allocation, registration, heartbeat, hub endpoints
- [ ] `05-tensor-contract.md` — `ITensorContract` implementation summary; input/output shapes
- [ ] `06-protobuf-api-schemas.md` — proto package references, REST/hub API surface
- [ ] `07-configuration-spec.md` — all `IOptions<>` sections, env vars, default values
- [ ] `08-port-network-map.md` — HTTP port, WS port, Docker network, DNS name
- [ ] `09-data-flow-sequences.md` — 3–5 Mermaid sequence diagrams for primary flows
- [ ] `10-state-persistence-model.md` — Redis keys, Postgres tables, IPFS CIDs used
- [ ] `11-security-model.md` — identity requirements, RBAC constraints, secrets
- [ ] `12-observability-telemetry.md` — metrics emitted, traces, events, SLO targets
- [ ] `13-performance-budget.md` — latency p95 targets, throughput, memory ceiling
- [ ] `14-qa-test-strategy.md` — test families, coverage target, contract test references
- [ ] `15-deployment-rollback.md` — Dockerfile EXPOSE, compose service, rollback procedure
- [ ] `16-runbook-oncall.md` — alert definitions, diagnosis steps, escalation
- [ ] `17-failure-modes-recovery.md` — top 5 failure modes with detection and recovery
- [ ] `18-changelog-migration.md` — session history, breaking changes, migration notes
- [ ] `19-session-progress-ledger.md` — per-session delivery log with dates and status
- [ ] `20-future-evolution-roadmap.md` — planned capabilities, dependency sessions

## Skills to Apply

```
.skills/system-architect.md          — module species doctrine, inter-species contracts
.skills/dotnet-devs.md               — module structure reference for accurate docs
.skills/machine-learning.md          — tensor contract per module
.skills/websockets-inferences.md     — hub endpoints and envelope schema per module
.skills/storage-data-management.md   — storage usage per module
.skills/models/model-t.md            — trader tensor contract
.skills/models/model-a.md            — arbitrager tensor contract
.skills/models/model-d.md            — defi tensor contract
.skills/designer.md                  — designer block and socket contracts
.skills/ai-hub.md                    — ai-hub plugin and canvas contract
.skills/hydra-collector.md           — data-layer collector contract
.skills/exchange-adapters.md         — broker exchange adapter contract
```

## Copilot Rules to Enforce

- Every document must have a `> **Status**:` header and `> **Last Updated**:` line
- `08-port-network-map.md` must match the port allocation table in `copilot-instructions.md`
- `05-tensor-contract.md` must reference the concrete `ITensorContract` implementation in the module's source
- Documents must be updated every session that affects the module (living documents)

## Acceptance Gates

- [ ] `docs/modules/` directory created with subdirectory per module
- [ ] Every module has all 20 documents (280 files total across 14 modules)
- [ ] Each `04-runtime-contract.md` matches the actual HTTP/WS ports in the codebase
- [ ] Each `14-qa-test-strategy.md` references the actual test project in `src/`
- [ ] 4 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `docs/bcg/module-document-pack-template.md` | Base template from Session 01 |
| `docs/bcg/module-species-doctrine.md` | Species classification reference |
| `src/` | Read actual module code before writing docs |
| `.github/copilot-instructions.md` | Port allocation table (authoritative) |
| `.prompts-update/BCG_Session_18_Extended_Document.md` | Full session spec |
