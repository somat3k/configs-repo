---
mode: agent
description: "BCG Session 15 — Observability, Telemetry, and Runtime Forensics"
status: "⏳ Pending — no structured telemetry or SLO dashboard layer exists"
depends-on: ["session-02", "session-05", "session-10", "session-14"]
produces: ["docs/bcg/session-15-*.md", "src/core/MLS.Core/Observability/"]
---

# Session 15 — Observability, Telemetry, and Runtime Forensics

> **Status**: ⏳ Pending — modules emit ad-hoc logs but no structured telemetry taxonomy, SLO targets, or forensic correlation model exists.

## Session Goal

Create a deep observability model: every block, kernel, tensor, transport path, queue, and module health signal is measurable, traceable, and forensically reconstructable across the entire BCG fabric.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-15-extended-document.md` (source: `.prompts-update/BCG_Session_15_Extended_Document.md`)
- [ ] `telemetry-taxonomy.md` — metrics / logs / traces / events taxonomy per subsystem
- [ ] `observability-schema.md` — schema for every metric, trace span, and event type; label conventions
- [ ] `incident-drillbook.md` — runbooks for: module offline, inference latency spike, storage degraded, tensor lineage loss
- [ ] `slo-dashboard-requirements.md` — SLO targets per session; alert thresholds; Grafana / OTEL dashboard specs
- [ ] `forensic-correlation-guide.md` — trace propagation through envelope chain, W3C traceparent, causation chain

### C# Observability Abstractions (`src/core/MLS.Core/Observability/`)
- [ ] `ITelemetryEmitter.cs` — `EmitMetric(MetricRecord)`, `EmitEvent(TelemetryEvent)`, `StartSpan(string name): ISpan`
- [ ] `MetricRecord.cs` — record: name, value, unit, tags, timestamp
- [ ] `TelemetryEvent.cs` — record: eventType, source, traceId, correlationId, timestamp, payload
- [ ] `ISpan.cs` — `SetTag(string, string)`, `SetStatus(SpanStatus)`, `End()`; wraps `System.Diagnostics.Activity`
- [ ] `SloTarget.cs` — record: metricName, p50, p95, p99, alertThreshold
- [ ] `ObservabilityHealthReporter.cs` — polls kernel registry and module health; emits `OBSERVABILITY_SUMMARY` every 30 s
- [ ] `TraceCorrelator.cs` — propagates W3C `traceparent` through `EnvelopePayload` and `Activity.Current`
- [ ] `ForensicAuditLog.cs` — queries `replay_log` and `audit_log` to reconstruct causation chain for an incident traceId
- [ ] Add `OBSERVABILITY_SUMMARY`, `SLO_BREACH`, `TRACE_CORRELATION_FAILED` to `MessageTypes`

### OpenTelemetry Integration
- [ ] Add `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Exporter.Console` NuGet packages to `MLS.Core`
- [ ] Configure `ActivitySource("bcg.runtime")` in `ObservabilityStartup.cs`
- [ ] Instrument Block Controller hub with trace spans: route start → admission → dispatch → ack
- [ ] Instrument ML Runtime inference: span from `InferenceEngine.RunAsync` entry to response

### Tests (`src/core/MLS.Core.Tests/Observability/`)
- [ ] `TraceCorrelatorTests.cs` — traceparent propagated through three envelope hops
- [ ] `ObservabilityHealthReporterTests.cs` — emits OBSERVABILITY_SUMMARY with correct module count
- [ ] `SloTargetTests.cs` — p95 breach triggers SLO_BREACH event

## Skills to Apply

```
.skills/beast-development.md         — System.Diagnostics.Activity, ActivitySource, perf counters
.skills/dotnet-devs.md               — IHostedService, IOptions<T>, OpenTelemetry SDK
.skills/system-architect.md          — telemetry taxonomy, SLO governance
.skills/storage-data-management.md   — forensic queries on replay_log and audit_log
.skills/websockets-inferences.md     — observability event streaming to operator dashboard
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — observability events via typed EnvelopePayload
- Trace must propagate via W3C `traceparent` in `EnvelopePayload.TraceId` (Session 04 extension)
- SLO targets from `bcg-master-session-schedule.md` must be recorded in `slo-dashboard-requirements.md`
- Forensic log MUST use `replay_log` from Session 10 infrastructure

## Acceptance Gates

- [ ] `TraceCorrelator` propagates W3C traceparent through a 3-hop envelope chain without loss
- [ ] `ObservabilityHealthReporter` emits `OBSERVABILITY_SUMMARY` every 30 s with correct data
- [ ] ML Runtime inference spans appear in OTEL console exporter
- [ ] All new tests pass: `dotnet test`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Observability/` | Create observability abstractions here |
| `src/core/MLS.Core/Contracts/EnvelopePayload.cs` | TraceId field (added in Session 04) |
| `src/block-controller/MLS.BlockController/` | Instrument with OTEL spans |
| `src/modules/ml-runtime/MLS.MLRuntime/Inference/InferenceEngine.cs` | Add inference spans |
| `infra/postgres/init/` | replay_log and audit_log tables (from Session 10 + 14) |
| `.prompts-update/BCG_Session_15_Extended_Document.md` | Full session spec |
