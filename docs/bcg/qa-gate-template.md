# QA Gate Template

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## 1. Purpose

This document defines the QA gate template that every BCG module must satisfy before advancing to production-grade status. The gate is not a one-time check. It is re-evaluated on every session that introduces changes to a module.

A module that fails any mandatory gate item may not be accepted as session-complete until the item is resolved or explicitly deferred with a tracking reference.

---

## 2. How to Use This Template

1. Copy the gate checklist (Section 6) into the module's QA document (`docs/modules/{name}/14-qa.md`).
2. Evaluate each item against the current state of the module.
3. Record the result (Pass / Fail / Deferred) and the evidence for each item.
4. Resolve all Fail items before closing the session, unless a formal deferral is recorded.
5. Submit the completed gate as part of the session close documentation.

---

## 3. Gate Categories

The QA gate has seven mandatory categories:

| # | Category | Weight |
|---|----------|--------|
| 1 | Test Coverage | Mandatory |
| 2 | Contract Verification | Mandatory |
| 3 | Health and Observability | Mandatory |
| 4 | Rollback Readiness | Mandatory |
| 5 | Operational Documentation | Mandatory |
| 6 | Performance Budget | Mandatory |
| 7 | Failure Mode Inventory | Mandatory |

All seven categories must pass for a module to reach production-grade status.

---

## 4. Deferral Policy

A gate item may be deferred if:

1. The item cannot be completed in the current session due to a dependency not yet implemented.
2. The item is not applicable to the current phase of the module's lifecycle (e.g., resilience tests cannot be written before the service is live).
3. The deferral is recorded with:
   - the item identifier
   - the reason for deferral
   - the target session for resolution
   - the name of the person authorizing the deferral

A module with open deferrals may not be labeled production-grade. It may be labeled session-complete with deferrals tracked.

---

## 5. Evidence Requirements

Each gate item requires evidence of completion. Acceptable evidence forms:

| Evidence Form | Examples |
|---------------|---------|
| Test file reference | `src/modules/ml-runtime/MLS.MLRuntime.Tests/InferenceEngineTests.cs` |
| CI run link | GitHub Actions workflow run URL |
| Document reference | `docs/modules/ml-runtime/17-failure-modes.md` |
| Benchmark result | BenchmarkDotNet output in `docs/architecture/performance-baselines.md` |
| Log sample | Structured log output showing the required field |
| Command output | `curl` response, `dotnet test` output |

---

## 6. QA Gate Checklist

Copy this checklist into `docs/modules/{name}/14-qa.md` and complete it per session.

```markdown
## QA Gate — {Module Name} — Session {N}

Date: {date}
Evaluator: {name or role}
Module version: {version}

---

### Category 1 — Test Coverage

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 1.1 Unit tests exist | ≥ 20 unit tests covering isolated component logic | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 1.2 Integration tests exist | ≥ 10 integration tests covering real dependency interactions | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 1.3 Contract tests exist | At least one test per declared input/output message type | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 1.4 Transport tests exist | Hub methods (SendEnvelope, ReceiveEnvelope) tested | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 1.5 Health probe tests | Liveness, readiness, and startup probes tested | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 1.6 Coverage target met | > 80% line coverage confirmed | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 1.7 All tests pass | `dotnet test` returns 0 failures | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 1 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Category 2 — Contract Verification

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 2.1 Input contract declared | All accepted message types and payload schemas documented | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 2.2 Output contract declared | All emitted message types and payload schemas documented | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 2.3 Tensor contract declared | Tensor classes, dtypes, shapes declared or marked "none" | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 2.4 Transport contract declared | HTTP, WebSocket, and any other transports documented | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 2.5 Envelope compliance | All inter-module messages use the envelope protocol | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 2.6 Version declared | Contract version is declared and ≥ 1 | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 2.7 No untyped payloads | No `object`, `dynamic`, or unversioned JSON in production paths | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 2 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Category 3 — Health and Observability

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 3.1 Liveness probe | GET /health/live returns 200 | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 3.2 Readiness probe | GET /health/ready returns 200 when ready, 503 when not | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 3.3 Heartbeat active | Heartbeat sends to Block Controller every 5 seconds | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 3.4 Structured logs | JSON-structured logs with trace ID, module ID, and log level | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 3.5 Metrics emitted | p50/p95/p99 metrics emitted for all critical operations | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 3.6 Trace ID propagated | Trace ID from envelope header propagated to all downstream calls | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 3.7 Error telemetry | Errors logged with exception details, trace ID, and module context | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 3 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Category 4 — Rollback Readiness

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 4.1 Rollback triggers documented | Specific thresholds that trigger a rollback are declared | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 4.2 Rollback procedure documented | Step-by-step rollback procedure exists in `15-deployment.md` | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 4.3 Rollback tested | Rollback procedure has been executed at least once in a non-production environment | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 4.4 Data integrity verified | Rollback does not corrupt or lose critical data | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 4.5 Block Controller deregistration | Module deregisters from Block Controller before stopping during rollback | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 4 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Category 5 — Operational Documentation

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 5.1 Charter complete | `01-charter.md` is complete and accurate | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 5.2 Architecture documented | `03-architecture.md` reflects the current implementation | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 5.3 Runtime contract documented | `04-runtime.md` is complete including startup and drain behavior | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 5.4 Runbook exists | `16-runbook.md` covers start, stop, health check, and common procedures | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 5.5 Diagrams current | `09-data-flows.md` reflects current data flows | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 5.6 Configuration documented | `07-configuration.md` covers all environment variables | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 5.7 Changelog updated | `18-changelog.md` reflects all changes in this session | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 5 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Category 6 — Performance Budget

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 6.1 Timing targets declared | p50, p95, p99 targets declared for all critical operations | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 6.2 Throughput targets declared | ops/sec targets declared for all throughput-sensitive flows | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 6.3 Targets measured | Benchmarks or load tests confirm targets are met | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 6.4 Degraded-state behavior declared | Performance expectations under dependency failure are documented | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 6.5 Backpressure configured | All `Channel<T>` instances use `BoundedChannelOptions` with declared `FullMode` | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 6.6 No blocking async | No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in async paths | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 6 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Category 7 — Failure Mode Inventory

| Item | Requirement | Result | Evidence |
|------|------------|--------|---------|
| 7.1 Inventory exists | `17-failure-modes.md` contains at least 3 documented failure modes | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 7.2 PostgreSQL failure covered | Behavior when PostgreSQL is unavailable is documented and tested | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 7.3 Redis failure covered | Behavior when Redis is unavailable is documented and tested | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 7.4 Block Controller failure covered | Behavior when Block Controller is unreachable is documented | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 7.5 Resilience tests exist | At least one test per documented failure mode verifies recovery | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |
| 7.6 Degraded mode tested | Module has been verified to operate in degraded mode | [ ] Pass / [ ] Fail / [ ] Deferred | {evidence} |

**Category 7 Result**: [ ] Pass / [ ] Fail
**Notes**: {notes}

---

### Gate Summary

| Category | Result |
|----------|--------|
| 1 — Test Coverage | [ ] Pass / [ ] Fail |
| 2 — Contract Verification | [ ] Pass / [ ] Fail |
| 3 — Health and Observability | [ ] Pass / [ ] Fail |
| 4 — Rollback Readiness | [ ] Pass / [ ] Fail |
| 5 — Operational Documentation | [ ] Pass / [ ] Fail |
| 6 — Performance Budget | [ ] Pass / [ ] Fail |
| 7 — Failure Mode Inventory | [ ] Pass / [ ] Fail |

**Overall Gate Result**: [ ] Pass / [ ] Fail

**Open Deferrals**:
| Item | Reason | Target Session | Authorized By |
|------|--------|---------------|---------------|
| {item} | {reason} | {session} | {name} |

**Module Status After Gate**:
[ ] Production-grade (all categories pass, no open deferrals)
[ ] Session-complete with deferrals (categories pass, open deferrals tracked)
[ ] Not accepted (one or more categories fail, not deferred)
```

---

## 7. Test Class Minimum Standards

### Unit Tests

- must be isolated (no real database, Redis, or HTTP calls)
- must use Moq for all external dependency interfaces
- must use FluentAssertions for all assertions
- must run in < 1 second per test in isolation
- naming: `{MethodName}_{Scenario}_{ExpectedResult}`

### Integration Tests

- may use real PostgreSQL, Redis, or in-memory equivalents
- must not depend on external APIs (use mocks or test doubles for external services)
- must clean up test data after each test

### Contract Tests

- must verify that the module correctly serializes and deserializes each declared message type
- must verify that the module rejects malformed payloads with typed errors
- must verify backward compatibility with the previous contract version when applicable

### Transport Tests

- must verify SignalR hub connection establishment
- must verify `SendEnvelope` and `ReceiveEnvelope` hub methods with real payloads
- must verify reconnection behavior

### Performance Tests

- must use BenchmarkDotNet for method-level timing
- must be located in `src/benchmarks/MLS.Benchmarks.csproj`
- must establish a baseline result before declaring a target met

### Resilience Tests

- must simulate each failure mode listed in `17-failure-modes.md`
- must verify that the module degrades gracefully (does not crash or corrupt state)
- must verify that the module recovers when the dependency becomes available again

---

## 8. Continuous Integration Requirements

Every module must have CI coverage for:

1. `dotnet build` — no errors
2. `dotnet test` — all tests pass
3. code coverage check — > 80%
4. contract lint — no unversioned payloads, no untyped envelopes
5. performance gate — benchmark regressions > 20% at p95 fail the build

CI workflow files must be present in `.github/workflows/` and must run on pull requests targeting the main branch.
