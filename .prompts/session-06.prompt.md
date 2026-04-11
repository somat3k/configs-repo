---
mode: agent
description: "BCG Session 06 — Batch Containers and Scheduler Performance Lanes"
status: "⏳ Pending — documentation and batch infrastructure required"
depends-on: ["session-05"]
produces: ["docs/bcg/session-06-*.md", "src/core/MLS.Core/Batch/", "src/block-controller/"]
---

# Session 06 — Batch Containers and Scheduler Performance Lanes

> **Status**: ⏳ Pending — no batch container infrastructure exists.

## Session Goal

Turn isolated inference and compute calls into managed batch flows with explicit queueing, backpressure, cancellation, fairness, and throughput measurement.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-06-extended-document.md` (source: `.prompts-update/BCG_Session_06_Extended_Document.md`)
- [ ] `batch-execution-spec.md` — container model, parallel/sequential/pipeline strategies
- [ ] `scheduler-strategy-guide.md` — fair-share, priority, deadline, capacity-weighted strategies
- [ ] `backpressure-design.md` — `BoundedChannel`, drop policy, throttle signals, producer feedback
- [ ] `queue-durability-rules.md` — in-memory vs Redis-backed queue, TTL, crash recovery
- [ ] `batch-benchmark-plan.md` — BenchmarkDotNet targets, throughput and latency targets

### C# Batch Abstractions (`src/core/MLS.Core/Batch/`)
- [ ] `IBatchContainer.cs` — `EnqueueAsync(BatchItem)`, `DrainAsync()`, `CancelAsync()`
- [ ] `BatchItem.cs` — record: batchId, priority, kernelRef, tensor input, deadline, traceId
- [ ] `BatchResult.cs` — record: batchId, outputs, duration, retryCount, errorClass
- [ ] `BatchContainerOptions.cs` — record: maxConcurrency, maxQueueDepth, timeoutMs, strategy
- [ ] `BatchStrategy.cs` — enum: Sequential, Parallel, Pipeline, PriorityWeighted
- [ ] `BatchScheduler.cs` — dispatches `IBatchContainer` instances with fair-share and priority lanes
- [ ] `InMemoryBatchContainer.cs` — `Channel<BatchItem>` backed implementation
- [ ] `BatchObservabilityRecord.cs` — record: start, end, queueDepth, throughput, droppedCount
- [ ] Emit `BATCH_STARTED`, `BATCH_COMPLETED`, `BATCH_CANCELLED`, `BATCH_ITEM_DROPPED` envelope events

### Block Controller Integration
- [ ] Add `BatchLaneRegistry.cs` — maps module capability to batch container + scheduler lane
- [ ] Route batch block requests through `BatchScheduler` in `KernelResolutionService`

### Benchmarks (`src/benchmarks/`)
- [ ] `BatchThroughputBenchmark.cs` — BenchmarkDotNet: 1000 items, parallel vs pipeline, measure p50/p95

### Tests (`src/core/MLS.Core.Tests/Batch/`)
- [ ] `BatchSchedulerTests.cs` — enqueue, drain, cancel, priority ordering
- [ ] `InMemoryBatchContainerTests.cs` — capacity limits, drop policy, backpressure signal
- [ ] `BatchObservabilityTests.cs` — metrics emitted on complete and cancel

## Skills to Apply

```
.skills/beast-development.md         — Channel<T>, BoundedChannelOptions, BenchmarkDotNet, GC Server mode
.skills/dotnet-devs.md               — IAsyncEnumerable<T>, CancellationToken, SemaphoreSlim
.skills/system-architect.md          — batch governance, scheduler policy, capacity planning
.skills/machine-learning.md          — tensor batch inputs aligned to ONNX batch dimension
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — batch events via typed EnvelopePayload
- All `Channel<T>` consumers MUST use `BoundedChannelOptions` with explicit `FullMode` policy
- Throughput target: design for > 1,000 ops/sec in optimized parallel lanes
- BenchmarkDotNet test REQUIRED for any method on the batch hot path

## Acceptance Gates

- [ ] `InMemoryBatchContainer` enqueues 1000 items and drains without deadlock
- [ ] Cancellation terminates active batch without data corruption
- [ ] Priority lane serves high-priority items before low-priority under load
- [ ] BenchmarkDotNet baseline committed to `src/benchmarks/`
- [ ] All tests pass: `dotnet test src/core/MLS.Core.Tests/`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Batch/` | Create all batch abstractions here |
| `src/core/MLS.Core/Kernels/` | Batch items resolve to kernels |
| `src/benchmarks/` | BenchmarkDotNet project |
| `src/block-controller/MLS.BlockController/Services/` | BatchLaneRegistry integration |
| `.prompts-update/BCG_Session_06_Extended_Document.md` | Full session spec |
