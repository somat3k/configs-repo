# Production Acceptance Framework

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## 1. Purpose

This document defines the production acceptance framework for the BCG ecosystem. It describes what it means for a module, a session, or the platform as a whole to be accepted as production-grade. It also defines the staged acceptance path from initial implementation to full production status.

The production acceptance framework is the final gate between development and live operation. It exists to protect runtime continuity, user trust, and data integrity.

---

## 2. Acceptance Levels

The BCG program defines four acceptance levels:

| Level | Name | Description |
|-------|------|-------------|
| L0 | Session-Complete | The session's deliverables exist and the QA gate is evaluated |
| L1 | Module-Verified | The module passes the full QA gate with no unresolved failures |
| L2 | Staging-Accepted | The module has passed integration tests in a staging environment |
| L3 | Production-Grade | The module is certified for live operation |

Each level builds on the previous. A module may not be promoted to a higher acceptance level without satisfying all requirements of the current level.

---

## 3. Level 0 — Session-Complete

A session is accepted as session-complete when:

### 3.1 Deliverables Exist

- all declared deliverables for the session are present in the repository
- all deliverables are in their correct locations (see session extended document for paths)
- documents are not stubs — they contain substantive content

### 3.2 QA Gate Evaluated

- the QA gate checklist (`docs/bcg/qa-gate-template.md`) has been completed for every module touched by the session
- all mandatory gate items are marked Pass or Deferred with a valid deferral record
- no mandatory gate item is marked Fail without an accompanying resolution plan

### 3.3 Code Builds and Tests Pass

- `dotnet build MLS.sln` exits with 0 errors
- `dotnet test` exits with 0 failures
- no new compilation warnings are introduced in session-changed files

### 3.4 Documents Updated

- every module touched by the session has its 20-document pack updated
- the changelog (`18-changelog.md`) for every touched module is updated
- the session progress ledger (`19-sessions.md`) is updated

---

## 4. Level 1 — Module-Verified

A module reaches Level 1 when:

### 4.1 Full QA Gate Pass

- all seven QA gate categories pass with no open deferrals
- test coverage is confirmed above 80%
- all test classes (unit, integration, contract, transport, health, smoke) exist and pass

### 4.2 Contract Completeness

- all input contracts, output contracts, tensor contracts, and transport contracts are declared
- all message types used by the module are declared as constants in `MLS.Core.Constants`
- all payload schemas are typed C# records or protobuf messages — no raw `object` or `dynamic`

### 4.3 Observability Confirmed

- the module emits structured logs in JSON format
- the module emits p50/p95/p99 metrics for all critical operations
- trace IDs from envelope headers are propagated to all downstream calls
- the health probe endpoints respond correctly under load

### 4.4 Performance Budget Satisfied

- all declared timing targets are met at p95 in a measured environment
- no performance violations (blocking async, unbounded channels, etc.) exist in the codebase
- BenchmarkDotNet results are published to `docs/architecture/performance-baselines.md`

### 4.5 Failure Mode Inventory Complete

- at least 3 failure modes are documented in `17-failure-modes.md`
- PostgreSQL, Redis, and Block Controller failure modes are covered
- at least one resilience test exists per documented failure mode

### 4.6 Rollback Verified

- rollback procedure is documented and has been executed at least once in a test environment
- rollback does not corrupt or lose data
- rollback restores health probe and Block Controller registration

---

## 5. Level 2 — Staging-Accepted

A module reaches Level 2 when it has passed Level 1 and:

### 5.1 Staging Environment Deployment

- the module has been deployed to a staging environment using the production Dockerfile
- the module registered with the Block Controller in staging and heartbeats are active
- all dependent modules are available in staging

### 5.2 Integration Verified in Staging

- the module has processed real (non-mocked) requests from other modules in staging
- all integration test scenarios have been executed against the staging environment
- no critical errors in the first 1 hour of staging operation

### 5.3 Performance Validated in Staging

- p95 timing targets are met in staging under representative load
- cache hit rate targets are met in staging
- no resource exhaustion (memory leak, CPU spike, file descriptor leak) observed over 24 hours

### 5.4 Smoke Tests Pass

- the module's smoke test suite passes against the staging environment
- the smoke test covers: start, registration, heartbeat, primary capability, and shutdown

### 5.5 Runbook Validated

- the runbook (`16-runbook.md`) has been executed by a person unfamiliar with the module
- all runbook procedures produce the expected outcomes in staging

### 5.6 Rollback Drill Completed

- the rollback procedure has been executed in staging at least once
- rollback completed within the documented maximum rollback duration
- Block Controller fabric remained operational during the rollback

---

## 6. Level 3 — Production-Grade

A module reaches Level 3 (production-grade) when it has passed Level 2 and:

### 6.1 Go/No-Go Review

A go/no-go review is conducted with the following attendees:

- module stewardship owner
- platform operations representative
- QA representative

The review checks:

| Check | Requirement |
|-------|------------|
| All Level 2 criteria satisfied | No open issues from Level 2 |
| No critical open deferrals | All deferrals resolved or formally accepted for post-launch |
| Performance sustained | No SLO breaches in staging over 24 hours |
| Rollback tested | Rollback drill completed successfully |
| Runbook validated | Runbook executable by unfamiliar operator |
| Security review | No critical security findings outstanding |
| Documentation complete | All 20 documents are in their final (non-stub) state |

### 6.2 Production Deployment

- deployment follows the rolling update procedure
- health probes are confirmed green before old version drains
- Block Controller capability registry is confirmed to show the new version
- no fan-out failure during deployment

### 6.3 Post-Deployment Validation (1-Hour Soak)

- health probes remain green for 1 hour after deployment
- no error rate increase above baseline during soak period
- p95 timing targets are met during soak period
- heartbeat active throughout soak period

### 6.4 Production Certification Record

A production certification record is created in `docs/modules/{name}/15-deployment.md` containing:

- certification date
- version certified
- go/no-go review participants
- soak period results
- certifying authority sign-off

---

## 7. Session Acceptance vs Module Acceptance

**Session acceptance** (Level 0) requires that all session deliverables exist and the QA gate is evaluated. It does not require that all modules are production-grade.

**Module acceptance** (Levels 1–3) is a separate lifecycle that progresses independently per module. A session may produce L0 session acceptance while some modules remain at L1 or L2.

The BCG program tracks both dimensions simultaneously:

- `docs/bcg/` tracks session-level acceptance
- `docs/modules/{name}/` tracks module-level acceptance
- `docs/bcg/bcg-master-session-schedule.md` reflects the overall program state

---

## 8. Platform Acceptance

The BCG platform as a whole reaches production-grade acceptance when:

1. all currently active modules have reached Level 3 (production-grade)
2. the Block Controller has reached Level 3
3. the fabric has sustained 24 hours of operation with all modules registered
4. the live runtime, hot refresh, and session joinability requirements (Session 11) are satisfied
5. the security identity and trust fabric (Session 14) is active
6. the observability and telemetry system (Session 15) is operational
7. the QA and release gate process (Session 17) is in regular use

---

## 9. Acceptance Records

### 9.1 Where Records Are Stored

| Acceptance Level | Record Location |
|-----------------|----------------|
| Session-Complete | `docs/bcg/session-{N}-extended-document.md` — Section: Acceptance Criteria |
| Module-Verified | `docs/modules/{name}/14-qa.md` — Gate Summary |
| Staging-Accepted | `docs/modules/{name}/15-deployment.md` — Staging Acceptance |
| Production-Grade | `docs/modules/{name}/15-deployment.md` — Production Certification |

### 9.2 Record Fields

Every acceptance record must contain:

- module name and version
- acceptance date
- acceptance level
- evaluator name or role
- open deferrals (if any)
- link to QA gate checklist result
- link to performance benchmark results

---

## 10. Regression Policy

Once a module has reached Level 3, a regression is defined as any change that causes:

- test failure in any category
- p95 timing target breach sustained for > 60 seconds in production
- health probe failure for > 30 seconds
- rollback trigger threshold exceeded

**Regression response**:

1. Immediately alert the stewardship owner.
2. Evaluate whether a rollback is required (compare to rollback triggers in `15-deployment.md`).
3. If rollback is not triggered, create a tracking issue and document the regression in `18-changelog.md`.
4. Re-run the affected QA gate categories within the next session.

A module that has regressed and has not been re-certified loses its production-grade status until re-certification is complete.

---

## 11. New Module Acceptance Path

When a new module species is declared:

1. Module is added to the module registry in `docs/bcg/bcg-master-session-schedule.md` with status `Reserved`.
2. The module's 20-document pack is initialized (all documents as stubs).
3. The first implementation session moves the module to `Active` status.
4. The module progresses through L0 → L1 → L2 → L3 across subsequent sessions.

No new module may be deployed to a shared environment before reaching Level 1.
