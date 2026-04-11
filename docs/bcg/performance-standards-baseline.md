# Performance Standards Baseline

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## 1. Purpose

This document establishes the performance standards baseline for the BCG ecosystem. Every module must declare how it performs relative to these standards. The baseline is a floor, not a ceiling. Individual modules may set stricter targets appropriate to their workload.

Performance standards are not optional. They are part of the quality gate every module must pass before reaching production-grade status.

---

## 2. How to Apply These Standards

1. **Read** the applicable sections for your module's workload class.
2. **Declare** your module's specific targets in its performance budget document (`docs/modules/{name}/13-performance.md`).
3. **Measure** using BenchmarkDotNet for method-level benchmarks and load tests for throughput targets.
4. **Monitor** p50, p95, and p99 in production via structured telemetry.
5. **Alert** when observed values exceed p99 targets for more than 60 seconds.

---

## 3. Runtime Performance Standards

These standards apply to all BCG modules.

### 3.1 Module Lifecycle

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Module cold boot (startup sequence complete) | < 15 s | < 30 s | < 45 s |
| Block Controller registration handshake | < 200 ms | < 500 ms | < 1 s |
| Graceful shutdown (drain + deregister) | < 5 s | < 15 s | < 30 s |

### 3.2 Health and Heartbeat

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Liveness probe response time | < 5 ms | < 20 ms | < 50 ms |
| Readiness probe response time | < 10 ms | < 50 ms | < 100 ms |
| Heartbeat delivery latency | < 100 ms | < 250 ms | < 500 ms |

### 3.3 HTTP API

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Simple read endpoint | < 10 ms | < 50 ms | < 100 ms |
| Write endpoint (with database) | < 20 ms | < 100 ms | < 200 ms |
| Health endpoint | < 5 ms | < 20 ms | < 50 ms |

### 3.4 WebSocket / SignalR

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Hub connection establishment | < 50 ms | < 200 ms | < 500 ms |
| Envelope delivery (hub to client) | < 10 ms | < 50 ms | < 100 ms |
| Topic subscription registration | < 20 ms | < 100 ms | < 250 ms |

---

## 4. Block Controller Performance Standards

The Block Controller is the critical path for the entire fabric. Its performance must exceed general module standards.

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Route decision (envelope received → decision) | < 2 ms | < 10 ms | < 25 ms |
| Module registration processing | < 50 ms | < 200 ms | < 500 ms |
| Capability registry query | < 1 ms | < 5 ms | < 10 ms |
| Health state query | < 1 ms | < 5 ms | < 10 ms |
| Heartbeat processing | < 5 ms | < 20 ms | < 50 ms |
| Broadcast to all registered modules | < 50 ms | < 200 ms | < 500 ms |

**Additional Block Controller Constraints**:

- No unbounded synchronous fan-out. All fan-out operations must be bounded and observable.
- Routing decisions must be deterministic. Given the same inputs, the same output must be produced.
- Heartbeat and capability state must be queryable and traceable at any time.
- No single routing decision may block other routing decisions (concurrent processing required).
- Route decision throughput: the Block Controller must sustain ≥ 1,000 routing decisions/sec.

---

## 5. Tensor and Runtime Performance Standards

### 5.1 Tensor Transformation

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Tensor boundary validation (shape/dtype check) | < 1 ms | < 5 ms | < 10 ms |
| Tensor lineage marker append | < 0.5 ms | < 2 ms | < 5 ms |
| Tensor serialization (< 64 KB, inline) | < 2 ms | < 10 ms | < 20 ms |
| Tensor deserialization (< 64 KB, inline) | < 2 ms | < 10 ms | < 20 ms |
| Tensor Redis cache write | < 5 ms | < 15 ms | < 30 ms |
| Tensor Redis cache read | < 3 ms | < 10 ms | < 20 ms |
| Tensor IPFS externalization (10–100 MB) | < 2 s | < 5 s | < 10 s |
| Tensor IPFS retrieval (10–100 MB) | < 2 s | < 5 s | < 10 s |

### 5.2 ONNX Inference

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| ONNX session creation | < 100 ms | < 500 ms | < 2 s |
| Inference (standard model, [1, 7] → [1, 3]) | < 2 ms | < 10 ms | < 20 ms |
| Inference (large model) | Declare per-model | Declare per-model | Declare per-model |
| Hot model swap (hot reload) | < 500 ms | < 1 s | < 2 s |
| Batch inference (32 samples, standard model) | < 10 ms | < 30 ms | < 50 ms |

### 5.3 Batch Processing

| Metric | Target |
|--------|--------|
| Batch throughput (optimized inference lane) | ≥ 1,000 ops/sec |
| Batch throughput (optimized transform lane) | ≥ 5,000 ops/sec |
| Queue drain rate under backpressure | ≥ producer rate |
| Retracing events in stable production paths | near-zero (< 1/hour) |
| Dynamic shape recalculation events | controlled — < 10/min |

---

## 6. Storage and Transport Performance Standards

### 6.1 PostgreSQL

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| Single-row read by primary key | < 2 ms | < 10 ms | < 20 ms |
| Single-row write (INSERT) | < 5 ms | < 30 ms | < 50 ms |
| Indexed query (100 rows) | < 5 ms | < 20 ms | < 50 ms |
| Transaction commit | < 5 ms | < 30 ms | < 60 ms |

### 6.2 Redis

| Operation | p50 Target | p95 Target | p99 Target |
|-----------|-----------|-----------|-----------|
| GET (hot key) | < 1 ms | < 5 ms | < 10 ms |
| SET (hot key) | < 1 ms | < 5 ms | < 10 ms |
| Cache hit rate for hot tensor state (steady) | > 90% | — | — |

### 6.3 IPFS

| Operation | p50 Target | p95 Target | Notes |
|-----------|-----------|-----------|-------|
| ADD (< 10 MB) | < 1 s | < 3 s | Model artifacts |
| ADD (10–100 MB) | < 5 s | < 15 s | Large tensors |
| CAT (< 10 MB) | < 500 ms | < 2 s | Hot artifact retrieval |
| CAT (10–100 MB) | < 2 s | < 8 s | Cold artifact retrieval |

### 6.4 System Uptime

| Metric | Target |
|--------|--------|
| Platform uptime (post-stabilization) | ≥ 99.95% |
| Block Controller uptime | ≥ 99.99% |
| Module uptime (non-critical) | ≥ 99.9% |
| Mean time to recovery (MTTR) | < 5 minutes |

---

## 7. ML Training Performance Standards

These apply to the TensorTrainer module (Session 08) and the ML Runtime training pipeline.

| Metric | Target |
|--------|--------|
| Training job scheduling latency | < 5 s from job submission to start |
| Training throughput (CPU-only, standard model) | Declare per-model |
| Checkpoint write latency | < 30 s |
| Artifact export latency | < 60 s |
| Artifact validation gate | < 120 s |
| Model promotion to runtime | < 300 s end-to-end |

---

## 8. UI and Operator Plane Performance Standards

| Operation | p50 Target | p95 Target |
|-----------|-----------|-----------|
| Initial page load | < 1 s | < 3 s |
| Graph canvas render (50 blocks) | < 100 ms | < 300 ms |
| Live event feed (100 events/sec) | no UI freeze | < 16 ms frame |
| Operator action (button click to response) | < 200 ms | < 500 ms |

**Additional UI Constraints**:

- Dashboards must not destabilize the runtime. Event subscriptions must use filtered, rate-limited feeds.
- Operator actions must be role-aware, auditable, and produce observable log entries.
- Live views must support event rate limits. Modules emitting more than 100 events/sec to the UI must implement server-side rate limiting or aggregation.
- No dashboard component may poll more frequently than every 1 second. Prefer push (WebSocket) over pull (HTTP polling).

---

## 9. Streaming Performance Standards

| Metric | Target |
|--------|--------|
| Streaming first event latency (p95) | < 250 ms |
| Streaming event-to-event interval jitter (p95) | < 50 ms |
| Maximum fan-out per topic | Declare per topic |
| Backpressure onset threshold | Declare per consumer |
| Stream resume after disconnect | < 5 s |

---

## 10. Benchmark Requirements

### When Benchmarks Are Required

BenchmarkDotNet benchmarks are required for any method on:

- the envelope routing hot path
- any tensor boundary enforcement point
- any ONNX inference call
- any channel read/write on a bounded `Channel<T>`
- any critical serialization or deserialization path

### Benchmark Location

All benchmarks live in `src/benchmarks/MLS.Benchmarks.csproj`.
Run with: `dotnet run -c Release --project src/benchmarks/MLS.Benchmarks.csproj`

### Benchmark Naming Convention

```csharp
[Benchmark]
public {ReturnType} {OperationName}_{ModuleName}()
```

### Benchmark Baseline Policy

- Benchmarks must establish a baseline result on the target environment.
- Benchmark regressions of more than 20% at p95 block release until investigated.
- Benchmark results must be published to `docs/architecture/performance-baselines.md` on every release.

---

## 11. Declared Performance Violations

The following patterns are prohibited and constitute performance governance violations:

| Violation | Rule |
|-----------|------|
| `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` in async paths | Always use `await`; never block async code synchronously |
| Unbounded `Channel<T>` without explicit `FullMode` | All channels must declare `BoundedChannelOptions` with `FullMode` |
| Synchronous PostgreSQL calls on hot paths | Use async EF Core methods exclusively |
| Tenant-less log flooding | All high-frequency events must be scoped and rate-limited |
| IPFS use for small payloads (< 10 MB) | Use Redis for hot tensors; IPFS only for large artifacts |
| Polling loops with < 100 ms interval without backoff | Use WebSocket push or exponential backoff |
| Uncontrolled tensor retracing | Shape and dtype changes must be governed and minimal |

---

## 12. Performance Review Cadence

- Every session that changes a critical path must run relevant benchmarks and update `docs/architecture/performance-baselines.md`.
- Performance budget documents (`13-performance.md`) for affected modules must be updated before the session closes.
- Regressions that cannot be remediated in the current session must be logged as technical debt in the module's roadmap (`20-roadmap.md`).
