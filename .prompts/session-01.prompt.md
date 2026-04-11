---
mode: agent
description: "BCG Session 01 — Governance Baseline and Tensorification Foundation"
status: "✅ Complete"
depends-on: []
produces: ["docs/bcg/"]
---

# Session 01 — Governance Baseline and Tensorification Foundation

> **Status**: ✅ Complete — all 10 governance documents exist in `docs/bcg/`.  
> Run this prompt to verify completeness or to extend/fix any missing piece.

## Session Goal

Establish the production rules, documentation framework, execution vocabulary, and system-wide tensorification mandate for the BCG ecosystem.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [x] `bcg-master-session-schedule.md` — 20-session program schedule with exit criteria per session
- [x] `session-01-extended-document.md` — Full session narrative and deliverable log
- [x] `terminology-glossary.md` — BCG vocabulary (Block, Kernel, Tensor, Envelope, Species, etc.)
- [x] `universal-tensor-foundation.md` — Foundational tensor mandate and field definitions
- [x] `module-species-doctrine.md` — Module independence and species classification rules
- [x] `module-document-pack-template.md` — 20-document template for every module
- [x] `performance-standards-baseline.md` — Program-wide p95 latency, throughput, and cache targets
- [x] `qa-gate-template.md` — Reusable QA gate checklist template
- [x] `production-acceptance-framework.md` — Criteria for module production promotion
- [x] `live-runtime-direction.md` — Hot-refresh, rolling update, and fabric continuity policy

### Validation Tasks
- [ ] Verify all 10 documents are present with `ls docs/bcg/`
- [ ] Verify `bcg-master-session-schedule.md` lists all 20 sessions with exit criteria
- [ ] Verify `universal-tensor-foundation.md` references `EnvelopePayload` fields and future Session 04 transport work
- [ ] Verify `module-document-pack-template.md` contains all 20 document slots

## Skills to Apply

```
.skills/system-architect.md          — module topology, envelope protocol
.skills/copilot-instruction-blueprint.md — naming standards, documentation structure
.skills/dotnet-devs.md               — C# 13, primary constructors, nullable refs
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — envelope fields are fixed
- All module documentation must follow the 20-document pack template

## Acceptance Gates

- [ ] `docs/bcg/` contains exactly these 10 files (+ any session N docs from later sessions)
- [ ] Every governance document has a version header and status line
- [ ] The 20-session schedule exists and all sessions have goal, deliverables, and exit criteria
- [ ] No untyped cross-module payload references in any document

## Key Source Paths

| Path | Purpose |
|------|---------|
| `docs/bcg/` | All session governance documents |
| `.prompts-update/BCG_Session_01_Extended_Document.md` | Source extended document for reference |
| `.prompts-update/BCG_Master_Session_Schedule.md` | Source master schedule for reference |
| `src/core/MLS.Core/Contracts/EnvelopePayload.cs` | Envelope wire contract (read-only reference) |
