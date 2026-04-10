# Module 20-Document Pack Template

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active — Template
> **Session**: 01
> **Last Updated**: 2026-04-10
> **Usage**: Copy this template to `docs/modules/{module-name}/` and complete all sections.

---

## How to Use This Template

1. Copy this file to `docs/modules/{canonical-module-name}/` as the root index.
2. Create each of the 20 documents listed below in the same directory.
3. Complete all required sections in each document.
4. Mark incomplete documents clearly with `[DRAFT]` or `[STUB]` in their titles.
5. Update this index on every session that modifies the module.

Documents in this pack are living documents. Every BCG session that touches a module must update the affected documents before the session is accepted as complete.

---

## Document Index

| # | Document | File | Status |
|---|----------|------|--------|
| 1 | Module Charter | `01-charter.md` | Required |
| 2 | Context and Scope | `02-scope.md` | Required |
| 3 | Architecture Overview | `03-architecture.md` | Required |
| 4 | Runtime Contract | `04-runtime.md` | Required |
| 5 | Tensor Contract | `05-tensor-contract.md` | Required |
| 6 | Protobuf and API Schemas | `06-schemas.md` | Required |
| 7 | Configuration Specification | `07-configuration.md` | Required |
| 8 | Port and Network Map | `08-network.md` | Required |
| 9 | Data Flow and Sequence Diagrams | `09-data-flows.md` | Required |
| 10 | State Model and Persistence | `10-state.md` | Required |
| 11 | Security Model | `11-security.md` | Required |
| 12 | Observability and Telemetry | `12-observability.md` | Required |
| 13 | Performance Budget | `13-performance.md` | Required |
| 14 | QA and Test Strategy | `14-qa.md` | Required |
| 15 | Deployment and Rollback | `15-deployment.md` | Required |
| 16 | Runbook and On-Call Guide | `16-runbook.md` | Required |
| 17 | Failure Modes and Recovery | `17-failure-modes.md` | Required |
| 18 | Change Log and Migration Notes | `18-changelog.md` | Required |
| 19 | Session Progress Ledger | `19-sessions.md` | Required |
| 20 | Future Evolution Roadmap | `20-roadmap.md` | Required |

---

## Document 01 — Module Charter

```markdown
# {Module Name} — Module Charter

> Version: 1.0.0 | Session: XX | Status: Active

## Charter Statement
{One paragraph. What does this module exist to do? What problem does it solve?
What is its place in the BCG ecosystem? Must be stable. If the charter changes
fundamentally, this is a different species.}

## Primary Responsibility
{One sentence.}

## Secondary Responsibilities
- {item}
- {item}
- {item}

## Explicitly Out of Scope
- {item — something this module does not own or do}
- {item}
- {item}

## Canonical Name
{kebab-case name, e.g., ml-runtime}

## Port Allocation
- HTTP: {port}
- WebSocket: {port}

## Stewardship Owner
{Team or role}
```

---

## Document 02 — Context and Scope

```markdown
# {Module Name} — Context and Scope

> Version: 1.0.0 | Session: XX | Status: Active

## Position in BCG Ecosystem
{Where does this module sit in the architecture? Which layer?
Presentation / Orchestration / Runtime / Transformation / Intelligence / Persistence}

## Dependency Map

### Block Controller
- registration: required
- heartbeat: 5 second interval

### BCG Module Dependencies
| Module | Capability Consumed | Transport |
|--------|-------------------|-----------|
| {module} | {capability} | {transport} |

### Infrastructure Dependencies
| System | Purpose | Connection |
|--------|---------|-----------|
| PostgreSQL | {purpose} | connection string from env |
| Redis | {purpose} | connection string from env |
| IPFS | {purpose} | {API endpoint} |

### External Dependencies
| System | Purpose |
|--------|---------|
| {external system} | {purpose} |

## Bounded Context
{Describe what data and processes this module owns and what it does not own.}

## Consumer Map
{Which modules consume this module's outputs?}
| Module | Capability Used | Transport |
|--------|----------------|-----------|
| {module} | {capability} | {transport} |
```

---

## Document 03 — Architecture Overview

```markdown
# {Module Name} — Architecture Overview

> Version: 1.0.0 | Session: XX | Status: Active

## Component Map

| Component | Class / Interface | Responsibility |
|-----------|-----------------|----------------|
| {name} | {class} | {responsibility} |

## Key Interfaces
{List primary interfaces with their method signatures at a high level.}

## Data Flow Summary
{Describe the primary data flows through the module in prose.
Reference Document 09 for detailed diagrams.}

## Configuration Dependencies
{Which configuration values drive the architecture?
E.g., model path, tensor threshold, batch size.}

## Architectural Decisions
{List significant architectural decisions and their rationale.}

| Decision | Rationale | Alternatives Considered |
|----------|-----------|------------------------|
| {decision} | {rationale} | {alternatives} |
```

---

## Document 04 — Runtime Contract

```markdown
# {Module Name} — Runtime Contract

> Version: 1.0.0 | Session: XX | Status: Active

## Startup Sequence
1. Load configuration from environment variables and appsettings.json
2. Connect to PostgreSQL (with retry, {max_attempts} attempts, {delay}s backoff)
3. Connect to Redis (optional — degrade gracefully if unavailable)
4. Register with Block Controller (POST capabilities, receive moduleId)
5. Subscribe to hub topics: {list topics}
6. Activate health probes
7. Signal readiness

## Health Model
- Liveness: GET /health/live — returns 200 if process is alive
- Readiness: GET /health/ready — returns 200 if all critical dependencies are reachable
- Startup: GET /health/startup — returns 200 when startup sequence is complete

## Heartbeat
- Interval: 5 seconds
- Destination: Block Controller hub, type: HEARTBEAT
- Missed threshold: 3 consecutive → Block Controller escalates module health state

## Shutdown and Drain
- SIGTERM triggers graceful shutdown
- Active requests drain for up to {N} seconds
- SignalR hub connections: notify then close
- Background workers: cancel and await
- Persistence flush: complete pending writes
- Deregister from Block Controller before exit

## Resource Footprint (Steady State)
- CPU: {typical}% / {peak}%
- Memory: {typical} MB / {peak} MB
- Network: {inbound} / {outbound}
- PostgreSQL connections: {pool size}
- Redis connections: {pool size}
```

---

## Document 05 — Tensor Contract

```markdown
# {Module Name} — Tensor Contract

> Version: 1.0.0 | Session: XX | Status: Active

## Tensor Participation
{Does this module participate in tensor execution lanes?}
[ ] Yes — see contracts below
[ ] No — TensorContract: none

## Input Tensor Contract

| Name | Class | Dtype | Shape | Required | Description |
|------|-------|-------|-------|----------|-------------|
| {name} | {class} | {dtype} | {shape} | Yes/No | {description} |

## Output Tensor Contract

| Name | Class | Dtype | Shape | Description |
|------|-------|-------|-------|-------------|
| {name} | {class} | {dtype} | {shape} | {description} |

## Lineage Behavior
- Does this module append lineage markers? Yes / No
- Lineage operation types appended: {list operation types}
- Lineage pass-through policy: {describe}

## Storage Threshold Behavior
- Tensors above 64 KB: {behavior}
- Tensors above 10 MB: {behavior}
- IPFS externalization: Yes / No

## Shape Mismatch Behavior
{What does this module do when it receives a tensor with unexpected shape or dtype?}
```

---

## Document 06 — Protobuf and API Schemas

```markdown
# {Module Name} — Protobuf and API Schemas

> Version: 1.0.0 | Session: XX | Status: Active

## HTTP API

### Base URL
http://{canonical-name}:{http-port}

### Endpoints

| Method | Path | Request Body | Response | Description |
|--------|------|-------------|----------|-------------|
| GET | /health/live | — | 200 | Liveness probe |
| GET | /health/ready | — | 200 / 503 | Readiness probe |
| {method} | {path} | {schema} | {schema} | {description} |

## SignalR Hub

### Hub Path
/hubs/{canonical-name}

### Hub Methods (Client → Server)
| Method | Parameters | Description |
|--------|-----------|-------------|
| SendEnvelope | EnvelopePayload | Receive an envelope from a client or module |

### Hub Methods (Server → Client)
| Method | Parameters | Description |
|--------|-----------|-------------|
| ReceiveEnvelope | EnvelopePayload | Push an envelope to connected clients |

## Message Types

| Type Constant | Direction | Payload Schema | Description |
|--------------|-----------|---------------|-------------|
| {MessageTypes.XX} | inbound/outbound | {schema} | {description} |

## Versioning Policy
- Current schema version: {version}
- Backward compatibility window: {sessions or dates}
- Breaking change policy: {describe}
```

---

## Document 07 — Configuration Specification

```markdown
# {Module Name} — Configuration Specification

> Version: 1.0.0 | Session: XX | Status: Active

## Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| BLOCK_CONTROLLER_URL | Yes | — | Block Controller HTTP base URL |
| BLOCK_CONTROLLER_HUB_URL | Yes | — | Block Controller SignalR hub URL |
| DATABASE_CONNECTION_STRING | Yes | — | PostgreSQL connection string |
| REDIS_CONNECTION_STRING | No | — | Redis connection string (optional) |
| {MODULE}_HTTP_PORT | Yes | {port} | HTTP server port |
| {MODULE}_WS_PORT | Yes | {port} | WebSocket server port |
| {variable} | {req} | {default} | {description} |

## appsettings.json Sections

```json
{
  "BlockController": {
    "HttpUrl": "",
    "HubUrl": ""
  },
  "{ModuleName}": {
    "{key}": "{value}"
  }
}
```

## Configuration Validation
{List required validations on startup. What fails fast vs degrades gracefully?}
```

---

## Document 08 — Port and Network Map

```markdown
# {Module Name} — Port and Network Map

> Version: 1.0.0 | Session: XX | Status: Active

## Port Allocation

| Interface | Protocol | Port | Description |
|-----------|----------|------|-------------|
| HTTP API | HTTP/1.1 | {port} | REST endpoints and health probes |
| WebSocket / SignalR | WS | {port} | Real-time hub |

## Network Membership
- Docker network: mls-network (bridge)
- Service name: {canonical-name}
- Internal hostname: {canonical-name} (within mls-network)

## Inbound Connections

| Source | Protocol | Port | Purpose |
|--------|----------|------|---------|
| block-controller | WS | {port} | Heartbeat and envelope delivery |
| {module} | HTTP | {port} | {purpose} |
| web-app | WS | {port} | Operator dashboard |

## Outbound Connections

| Destination | Protocol | Port | Purpose |
|------------|----------|------|---------|
| block-controller | WS | 6100 | Registration, heartbeat, envelope routing |
| postgresql | TCP | 5432 | Persistence |
| redis | TCP | 6379 | Cache (optional) |
| ipfs | HTTP | 5001 | Large artifact storage |
| {destination} | {protocol} | {port} | {purpose} |
```

---

## Document 09 — Data Flow and Sequence Diagrams

```markdown
# {Module Name} — Data Flow and Sequence Diagrams

> Version: 1.0.0 | Session: XX | Status: Active

## Primary Data Flows

### Flow 1 — {Flow Name}
{Description of the flow.}

```
{Mermaid sequence diagram}
sequenceDiagram
    participant A as {Actor A}
    participant B as {Module}
    participant C as {Actor C}
    A->>B: {message}
    B->>C: {message}
    C-->>B: {response}
    B-->>A: {response}
```

### Flow 2 — {Flow Name}
{Description}

## Error Flows

### Error Flow 1 — {Error Scenario}
{Description of what happens when this error occurs.}
```

---

## Document 10 — State Model and Persistence

```markdown
# {Module Name} — State Model and Persistence

> Version: 1.0.0 | Session: XX | Status: Active

## State Ownership

| State | Owner | Storage | TTL / Retention |
|-------|-------|---------|----------------|
| {state name} | {module} | PostgreSQL / Redis / Memory | {policy} |

## PostgreSQL Schema

### Table: {table_name}
| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| id | uuid | No | Primary key |
| created_at | timestamptz | No | Creation timestamp |
| {column} | {type} | {nullable} | {description} |

## Redis Usage

| Key Pattern | Type | TTL | Purpose |
|------------|------|-----|---------|
| {prefix}:{id} | {type} | {ttl}s | {purpose} |

## IPFS Usage

| Content Type | When Used | Reference Storage |
|-------------|-----------|------------------|
| {content type} | {when} | PostgreSQL column {column} |

## State Transitions
{Describe the primary state machine for this module's domain objects.}
```

---

## Document 11 — Security Model

```markdown
# {Module Name} — Security Model

> Version: 1.0.0 | Session: XX | Status: Active

## Authentication
{How does this module authenticate inbound requests?}

## Authorization
{What authorization rules apply? RBAC roles? Capability checks?}

## Secrets Management
{How are secrets provided? Environment variables, vault, HSM?}
- Database credentials: {method}
- API keys: {method}
- Private keys (if applicable): {method}

## Audit Logging
{What actions are logged for audit purposes?}

## Network Security
{TLS requirements, mTLS, internal vs external traffic policies}

## Known Attack Surfaces
{List the attack surfaces this module is aware of and mitigations in place.}
```

---

## Document 12 — Observability and Telemetry

```markdown
# {Module Name} — Observability and Telemetry

> Version: 1.0.0 | Session: XX | Status: Active

## Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| {metric_name} | Counter/Gauge/Histogram | {labels} | {description} |

## Structured Logs

| Event | Level | Fields | Description |
|-------|-------|--------|-------------|
| {event} | Info/Warn/Error | {fields} | {description} |

## Distributed Traces
- Trace propagation: trace ID from incoming envelope header
- Span naming convention: {convention}
- Key spans: {list}

## Health Events
{What health events does this module emit to the Block Controller?}

## Dashboards
{What operational dashboards should exist for this module?}
```

---

## Document 13 — Performance Budget

```markdown
# {Module Name} — Performance Budget

> Version: 1.0.0 | Session: XX | Status: Active

## Critical Operation Timing Targets

| Operation | p50 | p95 | p99 | Notes |
|-----------|-----|-----|-----|-------|
| {operation} | {ms} | {ms} | {ms} | {notes} |

## Throughput Targets

| Flow | Target (ops/sec) | Measurement Method |
|------|-----------------|-------------------|
| {flow} | {target} | {method} |

## Resource Constraints

| Resource | Limit | Action When Exceeded |
|----------|-------|---------------------|
| Memory | {limit} MB | {action} |
| CPU | {limit}% | {action} |
| Queue depth | {limit} items | Backpressure / drop |

## Steady-State vs Degraded-State Behavior
{Describe expected performance in both states.}

## BenchmarkDotNet Coverage
{Which operations have BenchmarkDotNet benchmarks? Where are they located?}
```

---

## Document 14 — QA and Test Strategy

```markdown
# {Module Name} — QA and Test Strategy

> Version: 1.0.0 | Session: XX | Status: Active

## Test Project
- Path: `src/modules/{canonical-name}/{ModuleName}.Tests/`
- Framework: xUnit + FluentAssertions + Moq
- Coverage target: > 80%

## Test Classes

| Class | Count | Location | Status |
|-------|-------|----------|--------|
| Unit | {count} | {path} | {status} |
| Integration | {count} | {path} | {status} |
| Contract | {count} | {path} | {status} |
| Transport | {count} | {path} | {status} |
| Performance | {count} | {path} | {status} |
| Resilience | {count} | {path} | {status} |

## Quality Gate Status
See `docs/bcg/qa-gate-template.md` for the gate definition.

| Gate | Status |
|------|--------|
| Documented interfaces | [ ] Pass / [ ] Fail |
| Contract verification | [ ] Pass / [ ] Fail |
| Health telemetry observed | [ ] Pass / [ ] Fail |
| Rollback procedure | [ ] Pass / [ ] Fail |
| Runbook | [ ] Pass / [ ] Fail |
| Performance budget | [ ] Pass / [ ] Fail |
| Failure mode inventory | [ ] Pass / [ ] Fail |

## CI Integration
{How are tests run in CI? Which workflows include this module?}
```

---

## Document 15 — Deployment and Rollback

```markdown
# {Module Name} — Deployment and Rollback

> Version: 1.0.0 | Session: XX | Status: Active

## Docker Image
- Dockerfile: `src/modules/{canonical-name}/Dockerfile`
- Base image: `mcr.microsoft.com/dotnet/aspnet:9.0`
- Exposed ports: {http-port}, {ws-port}
- Network: mls-network

## docker-compose Service
{Service name and relevant configuration.}

## Deployment Procedure
1. Build Docker image
2. Run tests (CI gate)
3. Push image to registry
4. Update docker-compose or Kubernetes manifest
5. Rolling update: new instance starts → health probe → Block Controller registration
6. Old instance drains → deregisters → stops

## Rollback Triggers
- Error rate exceeds {threshold}%
- Health probe failures exceed {N} consecutive checks
- Performance SLO breach for {duration}

## Rollback Procedure
1. {step}
2. {step}
3. Verify rollback by checking health probe and Block Controller registration

## Rollback Verification
{How to confirm rollback was successful.}
```

---

## Document 16 — Runbook and On-Call Guide

```markdown
# {Module Name} — Runbook and On-Call Guide

> Version: 1.0.0 | Session: XX | Status: Active

## Start the Module
```bash
docker-compose up {canonical-name}
```

## Stop the Module (Graceful)
```bash
docker-compose stop {canonical-name}
```

## Check Health
```bash
curl http://{canonical-name}:{http-port}/health/ready
```

## Common Operational Procedures

### Procedure 1 — {Procedure Name}
**Trigger**: {when to use this procedure}
**Steps**:
1. {step}
2. {step}

## Escalation Path
1. Check health probes
2. Check structured logs
3. Check Block Controller capability registry for this module's status
4. {escalation step}
```

---

## Document 17 — Failure Modes and Recovery

```markdown
# {Module Name} — Failure Modes and Recovery

> Version: 1.0.0 | Session: XX | Status: Active

## Failure Mode Inventory

| ID | Failure | Symptoms | Probability | Impact | Detection | Recovery |
|----|---------|----------|------------|--------|-----------|----------|
| FM-01 | PostgreSQL connection lost | DB errors in logs, queue drain | M | H | Health probe / error rate | Retry with backoff; degrade gracefully |
| FM-02 | Redis unavailable | Cache miss rate 100% | M | M | Log warning; metric spike | Continue without cache |
| FM-03 | Block Controller unreachable | Missed heartbeat alerts | L | H | Missed heartbeat counter | Retry registration; queue outbound messages |
| FM-{N} | {failure} | {symptoms} | L/M/H | L/M/H/C | {detection} | {recovery} |

## Recovery Drill Schedule
{How often are recovery drills executed for this module?}
```

---

## Document 18 — Change Log and Migration Notes

```markdown
# {Module Name} — Change Log and Migration Notes

> Version: 1.0.0 | Session: XX | Status: Active

## Changelog

### [1.0.0] — {date}
#### Added
- Initial species declaration

## Migration Notes

### Contract Changes
{List any breaking or non-breaking contract changes per version.}

### Consumer Action Required
{What do downstream consumers need to do for each breaking change?}
```

---

## Document 19 — Session Progress Ledger

```markdown
# {Module Name} — Session Progress Ledger

> Version: 1.0.0 | Session: XX | Status: Active

## Session History

| Session | Date | Changes | Documents Updated |
|---------|------|---------|------------------|
| 01 | 2026-04-10 | Species declaration | All 20 documents initialized |
| {session} | {date} | {changes} | {documents} |

## Current Status
- Production-grade: [ ] Yes / [ ] No
- Quality gate: [ ] Passed / [ ] Pending
- Document pack: [ ] Complete / [ ] Partial — {missing documents}
```

---

## Document 20 — Future Evolution Roadmap

```markdown
# {Module Name} — Future Evolution Roadmap

> Version: 1.0.0 | Session: XX | Status: Active

## Planned Capabilities

| Capability | Target Session | Priority | Description |
|-----------|---------------|---------|-------------|
| {capability} | Session {N} | High/Med/Low | {description} |

## Known Technical Debt

| Item | Impact | Target Session |
|------|--------|---------------|
| {item} | H/M/L | {session} |

## Performance Improvement Targets

| Area | Current | Target | Target Session |
|------|---------|--------|---------------|
| {area} | {current} | {target} | {session} |

## Contract Evolution Plans

| Contract | Planned Change | Breaking | Target Session |
|----------|---------------|---------|---------------|
| {contract} | {change} | Yes/No | {session} |
```
