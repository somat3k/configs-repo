---
mode: agent
description: "BCG Session 19 — Production Hardening and Capacity Validation"
status: "⏳ Pending — load testing, bottleneck remediation, and go/no-go criteria not established"
depends-on: ["session-17", "session-18"]
produces: ["docs/bcg/session-19-*.md", "src/tests/MLS.LoadTests/"]
---

# Session 19 — Production Hardening and Capacity Validation

> **Status**: ⏳ Pending — platform not yet stress-tested under realistic or extreme conditions.

## Session Goal

Stress, harden, and validate the platform under realistic and extreme operating conditions to identify and close all capacity, stability, and failure-mode gaps before go-live.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-19-extended-document.md` (source: `.prompts-update/BCG_Session_19_Extended_Document.md`)
- [ ] `capacity-plan.md` — resource envelopes per module: CPU, RAM, Redis memory, Postgres IOPS, IPFS storage
- [ ] `load-soak-results.md` — results from all load and soak tests (updated after each run)
- [ ] `bottleneck-remediation-matrix.md` — identified bottleneck → root cause → mitigation → re-test result
- [ ] `production-hardening-report.md` — final go/no-go assessment for each hardening category

### Load and Soak Test Suite (`src/tests/MLS.LoadTests/`)
- [ ] Create `MLS.LoadTests` project using NBomber or k6-compatible runner
- [ ] `BlockControllerLoadTest.cs` — 500 concurrent module registrations; measure route decision < 10 ms p95
- [ ] `InferenceLoadTest.cs` — 1,000 inference requests/s; measure p95 < 10 ms for model-t
- [ ] `BatchThroughputLoadTest.cs` — 50,000 batch items over 60 s; measure > 1,000 ops/s sustained
- [ ] `StreamFanOutLoadTest.cs` — 10,000 events to 100 subscribers; measure first event < 250 ms p95
- [ ] `StorageSoakTest.cs` — 24-hour Redis/Postgres write cycle; verify no memory leak, TTL respected
- [ ] `WebhookIngestionLoadTest.cs` — 10,000 webhook requests/s; verify reject rate < 0.1% for valid payloads

### Chaos and Failure Injection
- [ ] `KillModuleTest.cs` — kill Trader module; verify Block Controller detects within heartbeat window and reroutes
- [ ] `PartitionRedisTest.cs` — simulate Redis unavailable; verify optional Redis fallback, no crash
- [ ] `CorruptEnvelopeTest.cs` — send malformed envelope; verify rejection without poisoning healthy queue
- [ ] `SaturateBatchQueueTest.cs` — overflow batch queue; verify drop policy emits `BATCH_ITEM_DROPPED` and does not block

### Performance Remediation Pass
- [ ] Profile `InferenceEngine` under load — eliminate allocations on hot inference path (Span/ArrayPool)
- [ ] Profile `EnvelopePayload` serialization — confirm MessagePack path active for binary frames
- [ ] Profile `StrategyRouter` under 500 concurrent route requests — confirm < 10 ms p95
- [ ] Profile batch `Channel<T>` under saturation — confirm `BoundedChannelOptions.FullMode` drops without deadlock

### Infrastructure Capacity
- [ ] Validate Postgres connection pool under 500 concurrent write ops — confirm < 30 ms p95
- [ ] Validate Redis eviction policy under hot tensor load — confirm LRU or allkeys-lru configured
- [ ] Add `HEALTHZ` endpoint to all modules if not present — returns 200 when ready, 503 when degraded

## Skills to Apply

```
.skills/beast-development.md         — ArrayPool, Span, BenchmarkDotNet, ServerGC, throughput analysis
.skills/dotnet-devs.md               — CancellationToken, ConfigureAwait, async hot path
.skills/storage-data-management.md   — Postgres pool, Redis eviction, IPFS pin analysis
.skills/system-architect.md          — capacity planning, go/no-go criteria
.skills/networking.md                — load test tooling, concurrent connection analysis
```

## Copilot Rules to Enforce

- Performance targets from `bcg-master-session-schedule.md` are HARD gates — do not move them
- No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` in any code changed during remediation
- All `Channel<T>` with `BoundedChannelOptions` — unbounded channels are not allowed after Session 19
- `HEALTHZ` endpoint on every module — returning 200 is a certification requirement

## Acceptance Gates

- [ ] All 7 load tests pass at declared throughput and latency targets
- [ ] All 4 chaos tests: system degrades gracefully without crash
- [ ] Inference p95 < 10 ms measured under 1,000 rps sustained load
- [ ] Batch throughput > 1,000 ops/s measured over 60-second window
- [ ] Block Controller route decision p95 < 10 ms under 500 concurrent requests
- [ ] `load-soak-results.md` populated with actual measured values
- [ ] 4 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/tests/MLS.LoadTests/` | Create load test project here |
| `src/benchmarks/` | Existing BenchmarkDotNet project |
| `src/modules/ml-runtime/MLS.MLRuntime/Inference/InferenceEngine.cs` | Hot-path profiling target |
| `src/block-controller/MLS.BlockController/Services/StrategyRouter.cs` | Route decision profiling target |
| `src/core/MLS.Core/Batch/` | Batch queue saturation target |
| `.prompts-update/BCG_Session_19_Extended_Document.md` | Full session spec |
