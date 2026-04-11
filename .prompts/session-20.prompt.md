---
mode: agent
description: "BCG Session 20 — Go-Live Governance and Continuous Evolution"
status: "⏳ Pending — operational model, release cadence, and module admission process not established"
depends-on: ["session-17", "session-18", "session-19"]
produces: ["docs/bcg/session-20-*.md", "docs/operations/"]
---

# Session 20 — Go-Live Governance and Continuous Evolution

> **Status**: ⏳ Pending — final session. Defines the long-term operational constitution after initial production launch.

## Session Goal

Establish the stable operational model: release cadence, improvement loop governance, module admission process for new species, and the living documentation system that sustains the platform beyond initial go-live.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-20-extended-document.md` (source: `.prompts-update/BCG_Session_20_Extended_Document.md`)
- [ ] `go-live-governance-charter.md` — go-live definition, acceptance sign-off, launch day runbook
- [ ] `release-train-policy.md` — cadence (weekly/bi-weekly), freeze window, hotfix process, rollback SLA
- [ ] `continuous-improvement-model.md` — incident → RCA → backlog item → session slot → release
- [ ] `module-admission-handbook.md` — 12-step checklist to admit a new module species into production fabric
- [ ] `post-launch-audit-framework.md` — 30/60/90-day audit checkpoints, SLO review, documentation currency

### Operations Documentation (`docs/operations/`)
- [ ] `launch-runbook.md` — go-live day checklist: infra ready, module health, Block Controller governor active, smoke test suite
- [ ] `incident-response-playbook.md` — P0/P1/P2/P3 classification, response SLA, war room protocol
- [ ] `on-call-rotation-guide.md` — alert routing, escalation chain, after-hours response
- [ ] `change-management-process.md` — RFC template, review gate, approval tiers, rollout procedure
- [ ] `module-admission-checklist.md` — actionable version of `module-admission-handbook.md` with sign-off boxes

### Module Admission Checklist (12 Steps)
- [ ] Step 1: Module Charter submitted and reviewed
- [ ] Step 2: Port allocation registered and confirmed unique
- [ ] Step 3: 20-document pack complete (Session 18 format)
- [ ] Step 4: Unit test coverage ≥ 80%
- [ ] Step 5: Integration tests passing in CI
- [ ] Step 6: Contract tests passing (envelope + tensor + proto round-trip)
- [ ] Step 7: BlockController registration and heartbeat verified
- [ ] Step 8: HEALTHZ endpoint returning 200 verified
- [ ] Step 9: RBAC role declared and enforced
- [ ] Step 10: Observability schema submitted and emitting
- [ ] Step 11: Performance budget approved vs SLO targets
- [ ] Step 12: Chaos test passed (kill + recover within 30 s)

### Post-Launch Automation
- [ ] Add `module-audit.yml` GitHub Actions workflow — weekly: verifies 20-doc pack currency, test pass rate, port conflicts
- [ ] Add `slo-report.yml` — weekly: collects p95 metrics from OTEL; posts report to `docs/operations/slo-reports/`
- [ ] Add `doc-drift-check.yml` — verifies `19-session-progress-ledger.md` updated in last 30 days per module

### Living Document Review
- [ ] Update `docs/bcg/bcg-master-session-schedule.md` — mark all 20 sessions ✅ Complete
- [ ] Update every `19-session-progress-ledger.md` in `docs/modules/*/` with Session 20 delivery entry
- [ ] Verify `docs/bcg/` has all expected files from Sessions 01–20 (no gaps)
- [ ] Verify `docs/modules/*/` packs are complete and current

## Skills to Apply

```
.skills/system-architect.md          — operational constitution, governance model
.skills/dotnet-devs.md               — GitHub Actions YAML, workflow triggers
.skills/workflow-documentation.md    — living documentation, session ledger patterns
.skills/copilot-instruction-blueprint.md — documentation currency and update discipline
```

## Copilot Rules to Enforce

- Every new module admitted MUST pass all 12 admission steps — no partial admissions
- `bcg-master-session-schedule.md` MUST be updated to reflect final session completion status
- Documentation is a first-class deliverable — outdated docs are treated as bugs
- The platform has a stable operational constitution only when all 20 sessions are marked ✅

## Acceptance Gates

- [ ] All 5 governance documents committed to `docs/bcg/`
- [ ] `docs/operations/` directory created with 5 operational documents
- [ ] `module-audit.yml` and `slo-report.yml` GitHub Actions workflows exist and pass a dry run
- [ ] `bcg-master-session-schedule.md` shows all 20 sessions ✅ Complete
- [ ] All 14 modules have complete 20-document packs in `docs/modules/`
- [ ] Module admission checklist is a standalone runnable artifact at `docs/operations/module-admission-checklist.md`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `docs/bcg/bcg-master-session-schedule.md` | Update all sessions to ✅ Complete |
| `docs/modules/` | Per-module 20-doc packs from Session 18 |
| `docs/operations/` | Create operational documents here |
| `.github/workflows/` | Add module-audit and slo-report workflows |
| `.prompts-update/BCG_Session_20_Extended_Document.md` | Full session spec |
