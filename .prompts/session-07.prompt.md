---
mode: agent
description: "BCG Session 07 — DataEvolution Module"
status: "⏳ Pending — new module required"
depends-on: ["session-03", "session-04", "session-05"]
produces: ["docs/bcg/session-07-*.md", "src/modules/data-evolution/"]
---

# Session 07 — DataEvolution Module

> **Status**: ⏳ Pending — `data-evolution` module does not exist yet.

## Session Goal

Create the future-proof module that normalizes any structured, semi-structured, or unstructured input (files, streams, graphs) into BCG-native tensor and graph structures with full lineage preservation.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-07-extended-document.md` (source: `.prompts-update/BCG_Session_07_Extended_Document.md`)
- [ ] `data-evolution-module-charter.md` — module mission, port allocation (HTTP 5xxx/WS 6xxx — assign a free port), capabilities
- [ ] `transformation-grammar.md` — declarative rule language for field mapping, type coercion, normalization
- [ ] `schema-evolution-engine.md` — schema inference, drift detection, repair rules
- [ ] `source-adapter-framework.md` — CSV, JSON, Parquet, Arrow, binary stream adapter contracts
- [ ] `graph-communication-uplift-plan.md` — graph-to-tensor and stream-to-tensor conversion rules

### New Module (`src/modules/data-evolution/`)
- [ ] Scaffold `MLS.DataEvolution` .NET 9 project: `dotnet new webapi`
- [ ] Register with Block Controller on startup (HTTP POST /api/modules/register)
- [ ] Implement `BlockControllerClient` with 5-second heartbeat
- [ ] Implement `DataEvolutionHub` SignalR hub (`/hubs/data-evolution`)
- [ ] Implement `EnvelopeSender` — connects to Block Controller hub
- [ ] Create `ISourceAdapter` interface — `CanHandle(DataSourceDescriptor)`, `ReadAsync(source)`
- [ ] Implement `CsvSourceAdapter`, `JsonSourceAdapter` as starter adapters
- [ ] Create `ITransformationRule` interface — applies a mapping rule to a source record
- [ ] Create `TransformationEngine` — applies rule chain, emits lineage record per step
- [ ] Create `DataEvolutionController` — `POST /api/evolve` accepts source reference, returns evolved tensor ref
- [ ] Add `EVOLUTION_STARTED`, `EVOLUTION_COMPLETED`, `EVOLUTION_FAILED` to `MessageTypes`
- [ ] Add `EvolvedTensorPayload.cs` to `src/core/MLS.Core/Contracts/`
- [ ] Add Dockerfile `EXPOSE` for assigned HTTP and WS ports
- [ ] Add to `docker-compose.yml` and `MLS.sln`

### Tests (`src/modules/data-evolution/MLS.DataEvolution.Tests/`)
- [ ] `CsvSourceAdapterTests.cs` — parses CSV to typed records
- [ ] `TransformationEngineTests.cs` — rule chain applies in order, lineage accumulates
- [ ] `DataEvolutionHubTests.cs` — envelope sent on completion
- [ ] `BlockControllerClientTests.cs` — registration and heartbeat

## Skills to Apply

```
.skills/system-architect.md          — module species pattern, envelope discipline
.skills/dotnet-devs.md               — IAsyncEnumerable<T>, primary constructors, Channel<T>
.skills/machine-learning.md          — tensor normalization, dtype alignment
.skills/storage-data-management.md   — lineage in PostgreSQL, tensor ref in Redis/IPFS
.skills/python.md                    — source adapter may call Python for Parquet/Arrow parsing
.skills/websockets-inferences.md     — hub pattern, EnvelopeSender
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — all hub messages via typed EnvelopePayload
- Module MUST register with Block Controller on startup and heartbeat every 5 seconds
- Module MUST expose HTTP API AND WebSocket hub
- All blockchain addresses (if any transformation involves on-chain data) via PostgreSQL resource table — no hardcoded strings

## Acceptance Gates

- [ ] `POST /api/evolve` with a CSV input returns an `EvolvedTensorPayload` with lineage record
- [ ] `TransformationEngine` accumulates `TensorLineageRecord` steps
- [ ] Module registers with Block Controller and sends heartbeats
- [ ] All tests pass: `dotnet test src/modules/data-evolution/MLS.DataEvolution.Tests/`
- [ ] Module listed in `docker-compose.yml` and `MLS.sln`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/modules/data-evolution/` | New module root |
| `src/core/MLS.Core/Tensor/TensorLineageRecord.cs` | Lineage accumulation type |
| `src/core/MLS.Core/Contracts/Tensor/` | Add EvolvedTensorPayload here |
| `src/modules/trader/MLS.Trader/Services/BlockControllerClient.cs` | Reference implementation for BlockControllerClient pattern |
| `.prompts-update/BCG_Session_07_Extended_Document.md` | Full session spec |
