---
mode: agent
description: "BCG Session 17 — QA, Certification, and Release Gates"
status: "⏳ Pending — no cross-module QA governance or certification matrix exists"
depends-on: ["session-05", "session-09", "session-15"]
produces: ["docs/bcg/session-17-*.md", "src/tests/"]
---

# Session 17 — QA, Certification, and Release Gates

> **Status**: ⏳ Pending — individual modules have tests but no unified QA governance, certification checklist, or release gate matrix exists.

## Session Goal

Make quality assurance a hard production function: every module must pass a defined certification matrix before promotion, performance is tied to budgets, and failure injection is part of the release process.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-17-extended-document.md` (source: `.prompts-update/BCG_Session_17_Extended_Document.md`)
- [ ] `qa-governance-manual.md` — release philosophy, test families, ownership, and escalation
- [ ] `module-certification-checklist.md` — per-module checklist: unit, integration, contract, health, performance, chaos
- [ ] `release-gate-matrix.md` — gate table: which test families block promotion to prod for each module type
- [ ] `performance-test-catalog.md` — BenchmarkDotNet catalog: throughput, latency p50/p95/p99, serialization overhead
- [ ] `chaos-engineering-entry-set.md` — starter chaos scenarios: kill module, corrupt envelope, partition Redis, saturate batch queue

### Integration Test Project (`src/tests/`)
- [ ] Create `MLS.IntegrationTests` project — xUnit + `Aspire.Hosting.Testing`
- [ ] `BlockControllerIntegrationTests.cs` — full register → route → heartbeat → drain cycle
- [ ] `KernelExecutionIntegrationTests.cs` — block request → kernel resolved → output returned
- [ ] `BatchThroughputIntegrationTests.cs` — 1000 batch items processed in < 60 s
- [ ] `TensorLineageIntegrationTests.cs` — tensor routed through 3 modules preserves lineage
- [ ] `EnvelopeRoundTripTests.cs` — envelope serialized + deserialized preserves all fields including TraceId
- [ ] `StreamSoakTests.cs` — 10,000 events streamed to 10 subscribers in < 5 min without dropped messages
- [ ] `WebhookIngestionSanityTests.cs` — malformed JSON rejected in < 100 ms; valid payload routed correctly

### Contract Tests (`src/tests/MLS.ContractTests/`)
- [ ] `ProtobufRoundTripTests.cs` — all proto message types serialize and deserialize without field loss
- [ ] `VersionToleranceTests.cs` — consumer ignores unknown fields added in newer schema version
- [ ] `EnvelopeParserTests.cs` — envelope parseable before payload body is fully materialized

### Performance Benchmarks (`src/benchmarks/`)
- [ ] `EnvelopeSerializationBenchmark.cs` — MessagePack vs JSON; target < 1 µs per envelope on hot path
- [ ] `InferenceLatencyBenchmark.cs` — ONNX inference p95 < 10 ms for model-t [1,7] input
- [ ] `KernelThroughputBenchmark.cs` — pure kernel executions > 10,000 ops/s single core
- [ ] Add `BenchmarkDotNet` to `src/benchmarks/MLS.Benchmarks.csproj` if not already present

### CI Pipeline (`.github/workflows/`)
- [ ] Add `qa-gates.yml` — runs integration tests on PR to main; blocks merge on failure
- [ ] Add `benchmarks.yml` — runs BenchmarkDotNet on schedule; posts results as PR comment
- [ ] Add `chaos.yml` — runs chaos scenarios in staging environment on release tag

## Skills to Apply

```
.skills/xunit.md                     — xUnit, FluentAssertions, Moq patterns
.skills/dotnet-devs.md               — Aspire.Hosting.Testing, test project structure
.skills/beast-development.md         — BenchmarkDotNet, GC Server, throughput measurement
.skills/system-architect.md          — contract testing, certification matrix governance
.skills/machine-learning.md          — ONNX inference benchmark alignment
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — contract tests verify envelope invariants
- It is UNACCEPTABLE to remove or edit unrelated tests to make new tests pass
- BenchmarkDotNet test REQUIRED for envelope serialization and inference hot paths
- `Aspire.Hosting.Testing` for all AppHost integration tests — not manual HTTP calls

## Acceptance Gates

- [ ] All integration tests pass: `dotnet test src/tests/MLS.IntegrationTests/`
- [ ] Envelope serialization benchmark < 1 µs p95 (MessagePack)
- [ ] ONNX inference p95 < 10 ms for model-t
- [ ] `qa-gates.yml` CI workflow exists and blocks PR merge on failure
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/tests/` | Create MLS.IntegrationTests here |
| `src/benchmarks/` | BenchmarkDotNet project |
| `src/core/MLS.Core.Tests/` | Existing unit tests (do not break) |
| `.github/workflows/` | CI pipeline additions |
| `.prompts-update/BCG_Session_17_Extended_Document.md` | Full session spec |
