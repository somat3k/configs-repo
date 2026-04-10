# Module Species Doctrine

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## 1. Purpose

This document defines the module species governance model for the BCG ecosystem. Every module in the BCG platform is not merely a folder, service, or container. It is a separately governed living species with its own identity, runtime contract, tensor and transport contracts, quality assurance requirements, and evolution path.

This doctrine applies to all existing modules immediately and to all future modules from the moment of their declaration.

---

## 2. Why Module Species Governance

Without species governance, modules accumulate incompatible patterns over time:

- different heartbeat intervals across modules
- different error handling conventions
- different observability levels
- different deployment assumptions
- different testing depths
- different documentation completeness

Species governance prevents this fragmentation by requiring every module to satisfy the same five-category framework before reaching production-grade status.

---

## 3. The Five Species Categories

Every module must define and maintain documentation for all five categories:

1. **Species Identity** — what the module is and what it owns
2. **Species Runtime** — how the module behaves during its lifecycle
3. **Species Contract** — what the module accepts and produces
4. **Species Assurance** — how the module is tested, monitored, and recovered
5. **Species Evolution** — how the module changes over time

---

## 4. Category 1 — Species Identity

### 4.1 Canonical Name

Every module has a single canonical name in kebab-case (e.g., `block-controller`, `ml-runtime`, `data-evolution`). This name is used in:

- Docker service names
- network hostnames
- Block Controller registration
- capability registry keys
- documentation file names
- environment variable prefixes

The canonical name is permanent once assigned. Renaming a module requires a migration note and a new species declaration.

### 4.2 Charter

A charter is a short founding statement (one paragraph maximum) that answers:

- What does this module exist to do?
- What problem does it solve?
- What is its place in the BCG ecosystem?

A charter must be stable. If the charter changes fundamentally, the module is a different species.

### 4.3 Bounded Responsibility

A module must declare what it is responsible for and, equally important, what it is not responsible for. Unbounded responsibility is a sign of architectural erosion.

**Required declarations**:
- primary responsibility (one sentence)
- secondary responsibilities (up to five items)
- explicit out-of-scope items (at least three items)
- known boundary conditions

### 4.4 Dependency Map

A dependency map lists every external system and internal module that this module depends on:

- Block Controller (always required — heartbeat registration target)
- other BCG modules (by canonical name and capability consumed)
- databases (PostgreSQL, Redis, IPFS)
- external APIs or services
- Python runtimes or ML model files

Undeclared dependencies are governance violations.

---

## 5. Category 2 — Species Runtime

### 5.1 Startup Behavior

Every module must document its startup sequence:

1. configuration load order (environment variables, `appsettings.json`)
2. infrastructure connections (PostgreSQL, Redis) with fallback behavior
3. Block Controller registration — the module sends its moduleId, canonical name, HTTP port, WebSocket port, and capability list
4. health check activation (before or after registration)
5. topic subscriptions (SignalR hub topics that the module subscribes to)
6. readiness signal — when the module considers itself fully operational

**Rule**: A module must not process production traffic before completing Block Controller registration.

### 5.2 Health Model

Every module must define:

- liveness probe (path: `/health/live`) — is the process alive?
- readiness probe (path: `/health/ready`) — is the module ready to accept traffic?
- startup probe (path: `/health/startup`) — has startup completed?
- health escalation policy (what happens when the module fails to respond to the Block Controller within N missed heartbeats)
- degraded state definition (the module is alive but not at full capacity)

**Heartbeat discipline**:
- heartbeat interval: 5 seconds
- heartbeat endpoint: Block Controller hub, `ReceiveEnvelope` with type `HEARTBEAT`
- missed heartbeat threshold: 3 consecutive missed beats triggers health escalation

### 5.3 Routeability

A module declares:

- which capabilities it can serve and their current health status
- which capabilities require a warm cache or pre-loaded model
- what routing hints (affinity, priority, load constraints) the Block Controller may use

### 5.4 Shutdown and Drain Behavior

Every module must document:

- graceful shutdown trigger (SIGTERM handling)
- in-flight request drain time (maximum seconds to complete active requests)
- drain behavior for SignalR hub connections (notify connected clients, then close)
- drain behavior for background workers (signal cancellation, await completion)
- persistence flush (any pending writes to PostgreSQL or Redis must complete before process exit)
- deregistration from Block Controller (send deregistration envelope before exiting)

### 5.5 Resource Footprint

Every module must declare its expected steady-state resource footprint:

- CPU: typical and peak percentage per core
- Memory: typical heap and peak heap in MB
- Network: expected inbound and outbound bandwidth
- Disk: any local cache or temporary storage requirements
- External connections: number of PostgreSQL connections, Redis connections

---

## 6. Category 3 — Species Contract

### 6.1 Input Contract

The input contract lists all messages, envelopes, HTTP endpoints, and SignalR hub methods that the module accepts:

- message type (from `MLS.Core.Constants.MessageTypes`)
- transport (HTTP, WebSocket/SignalR, protobuf/gRPC)
- payload schema (typed C# record or protobuf message)
- version compatibility notes
- validation rules and rejection behavior

### 6.2 Output Contract

The output contract lists all messages, envelopes, events, and HTTP responses that the module produces:

- message type
- transport
- payload schema
- version
- target topics or modules
- error envelope formats

### 6.3 Tensor Contract

For modules that participate in advanced execution lanes:

- declared input tensor classes, dtypes, and shapes
- declared output tensor classes, dtypes, and shapes
- lineage preservation policy (does this module append lineage markers?)
- storage threshold behavior (does this module externalize large tensors?)
- validation behavior on shape or dtype mismatch

Modules that do not participate in tensor flows must declare `TensorContract: none` explicitly.

### 6.4 Transport Contract

Every module must document its transport configuration:

| Transport | Details |
|-----------|---------|
| HTTP | base URL, port, authentication, timeout, retry |
| WebSocket / SignalR | hub path, authentication, reconnect policy |
| gRPC / protobuf | package, service name, version, TLS requirements |
| Webhooks | outbound endpoint patterns, authentication, retry |

Fallback behavior must be declared for every transport (what happens if the transport is unavailable?).

### 6.5 Compatibility Notes

- minimum Block Controller version required
- minimum version of each dependency module
- breaking vs non-breaking change policy for each contract
- backward compatibility window

---

## 7. Category 4 — Species Assurance

### 7.1 Tests

Every module must maintain the following test categories:

| Test Class | Minimum Required | Purpose |
|------------|-----------------|---------|
| Unit tests | 20+ | Isolated component logic |
| Integration tests | 10+ | Component interaction with real dependencies |
| Contract tests | Per endpoint/message type | Verify input/output contract compliance |
| Transport tests | Per transport type | WebSocket, HTTP, SignalR hub behavior |
| Performance tests | Per critical path | BenchmarkDotNet or load test suite |
| Resilience tests | Per failure mode | Behavior under dependency failure |
| Health checks | Per health probe | Liveness, readiness, startup probes |
| Smoke checks | End-to-end happy path | Post-deployment verification |
| Recovery drills | Per documented failure mode | Validate recovery procedures |

Test framework: xUnit + FluentAssertions + Moq.
Coverage target: > 80% line coverage.

### 7.2 Telemetry

Every module must emit:

- structured logs (JSON format) for all state transitions and errors
- metrics for p50/p95/p99 of all critical operations
- distributed traces with propagated trace IDs from envelope headers
- health event emissions to the Block Controller on state change
- custom counters for domain-specific events (e.g., orders placed, inferences served, tensors processed)

### 7.3 Performance Budget

A module's performance budget declares timing targets for every critical operation:

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| (operation name) | (ms) | (ms) | (ms) |

Modules also declare:
- throughput target (operations per second at steady state)
- degraded-state performance (what happens when dependencies are slow)
- queue depth limits and backpressure thresholds

### 7.4 Rollback Notes

Every module must document:

- what triggers a rollback decision (error rate threshold, health probe failure, performance SLO breach)
- rollback procedure (step-by-step)
- expected rollback duration
- data integrity considerations during rollback
- how to verify rollback success

### 7.5 Failure Modes

Every module must maintain a failure mode inventory:

| Failure | Symptoms | Probability | Impact | Detection | Recovery |
|---------|----------|------------|--------|-----------|----------|
| (failure name) | (observable symptoms) | (L/M/H) | (L/M/H/C) | (how detected) | (recovery action) |

This inventory is not a theoretical exercise. It must be derived from actual dependency analysis and tested via resilience test cases.

---

## 8. Category 5 — Species Evolution

### 8.1 Changelog

Every module maintains a changelog that records every significant change:

```
## [version] — [date]
### Added
- (new capability or feature)
### Changed
- (modified behavior)
### Removed
- (removed capability)
### Fixed
- (bug fix)
### Security
- (security-relevant change)
```

### 8.2 Migration Notes

When a module changes its contract, the migration note documents:

- what changed (contract section, message type, schema field)
- breaking vs non-breaking classification
- backward compatibility window (sessions or dates)
- consumer action required (update payload schema, change topic subscription, etc.)
- migration verification procedure

### 8.3 Future Roadmap

Every module maintains a roadmap that declares:

- capabilities planned for future sessions
- known technical debt to be addressed
- planned performance improvements
- planned contract extensions

The roadmap is not a commitment. It is a declared direction that informs future session planning.

### 8.4 Stewardship Owner

Every module declares a stewardship owner:

- primary owner (team or role)
- escalation path
- review cadence (how often the 20-document pack is reviewed and updated)

---

## 9. Production-Grade Status Criteria

A module reaches production-grade status when all five species categories are complete and the following checks pass:

| Check | Requirement |
|-------|------------|
| Species Identity | Charter, bounded responsibility, and dependency map are complete |
| Species Runtime | Startup, health, drain, and footprint are documented |
| Species Contract | All input, output, tensor, and transport contracts are declared |
| Species Assurance | All required test classes exist and coverage target is met |
| Species Evolution | Changelog and stewardship owner are assigned |
| Quality Gate | Full QA gate template is satisfied (see `qa-gate-template.md`) |
| Production Acceptance | Production acceptance framework is satisfied (see `production-acceptance-framework.md`) |

---

## 10. Registered Module Species

| Module | Canonical Name | Status | Sessions |
|--------|---------------|--------|---------|
| Block Controller | `block-controller` | Active Species | All |
| Web App | `web-app` | Active Species | All |
| Designer | `designer` | Active Species | 4–6, 16 |
| Trader | `trader` | Active Species | — |
| Arbitrager | `arbitrager` | Active Species | — |
| DeFi | `defi` | Active Species | — |
| ML Runtime | `ml-runtime` | Active Species | 9 |
| Data Layer | `data-layer` | Active Species | 10 |
| AI Hub | `ai-hub` | Active Species | 2, 16 |
| Broker | `broker` | Active Species | — |
| Transactions | `transactions` | Active Species | — |
| Shell VM | `shell-vm` | Active Species | 11, 14 |
| TensorTrainer | `tensor-trainer` | Reserved | 8 |
| DataEvolution | `data-evolution` | Reserved | 7 |

---

## 11. New Module Admission

A new module species may be admitted to the BCG ecosystem only if:

1. a charter and bounded responsibility statement are written
2. the port allocation is registered (HTTP port and WebSocket port)
3. the dependency map is declared
4. the initial tensor and transport contract drafts are complete
5. the module is declared in the BCG master session schedule

New modules may not begin implementation without passing the new species admission check.
