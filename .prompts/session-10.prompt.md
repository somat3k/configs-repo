---
mode: agent
description: "BCG Session 10 — Storage and State Discipline"
status: "⏳ Pending — storage governance documentation and enforcement required"
depends-on: ["session-03", "session-06", "session-09"]
produces: ["docs/bcg/session-10-*.md", "src/core/MLS.Core/Storage/"]
---

# Session 10 — Storage and State Discipline

> **Status**: ⏳ Pending — Redis/Postgres/IPFS responsibilities are partially implied but not formally governed.

## Session Goal

Formalize Redis, PostgreSQL, and IPFS as distinct governed storage tiers with explicit ownership boundaries, retention policies, replay semantics, and storage failure handling.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-10-extended-document.md` (source: `.prompts-update/BCG_Session_10_Extended_Document.md`)
- [ ] `storage-governance-matrix.md` — what lives in Redis vs Postgres vs IPFS, ownership per module
- [ ] `tensor-cache-design.md` — hot tensor Redis schema, TTL policy, invalidation rules, > 90% hit target
- [ ] `state-persistence-model.md` — control-plane state in Postgres, write policy, row ownership
- [ ] `replay-recovery-design.md` — event log in Postgres, Redis snapshot, IPFS archive, restore procedure
- [ ] `artifact-retention-policy.md` — model artifact TTL, IPFS pin lifecycle, Postgres audit trail

### C# Storage Abstractions (`src/core/MLS.Core/Storage/`)
- [ ] `StorageTier.cs` — enum: Redis, Postgres, Ipfs (mirrors `TensorStorageMode`)
- [ ] `IStateStore.cs` — `GetAsync<T>`, `SetAsync<T>`, `DeleteAsync`, `ExistsAsync`
- [ ] `IReplayLog.cs` — `AppendAsync(LogEntry)`, `ReadFromAsync(sequenceId)`, `SnapshotAsync()`
- [ ] `LogEntry.cs` — record: sequenceId, eventType, moduleId, timestamp, payloadJson, traceId
- [ ] `StorageRoutingPolicy.cs` — static helper: `RouteFor(TensorStorageMode)` → `StorageTier`
- [ ] `ArtifactRetentionPolicy.cs` — record: ttlDays, pinToCold, requiresAuditTrail, autoExpire
- [ ] `StorageHealthChecker.cs` — pings Redis, Postgres, IPFS; emits `STORAGE_HEALTH_DEGRADED` event
- [ ] Add `STORAGE_HEALTH_DEGRADED`, `REPLAY_LOG_APPENDED`, `ARTIFACT_PINNED`, `ARTIFACT_EXPIRED` to `MessageTypes`

### Module Alignment
- [ ] Audit all modules for Redis usage pattern — confirm `sp.GetService<IConnectionMultiplexer>()` (optional) pattern
- [ ] Audit ML Runtime `InferenceEngine` Redis cache key format — document it in `tensor-cache-design.md`
- [ ] Ensure Block Controller stores module state in Postgres (not only in-memory)
- [ ] Add `IReplayLog` integration to Block Controller for module registration and route events

### Infrastructure (`infra/postgres/init/`)
- [ ] Add `replay_log` table migration: `(sequence_id BIGSERIAL, event_type TEXT, module_id UUID, timestamp TIMESTAMPTZ, payload JSONB, trace_id TEXT)`
- [ ] Add `artifact_registry` table migration: `(id UUID, artifact_key TEXT, ipfs_cid TEXT, created_at TIMESTAMPTZ, expires_at TIMESTAMPTZ, pinned BOOLEAN)`

### Tests (`src/core/MLS.Core.Tests/Storage/`)
- [ ] `StorageRoutingPolicyTests.cs` — maps TensorStorageMode to StorageTier correctly
- [ ] `StorageHealthCheckerTests.cs` — emits DEGRADED event when Redis unavailable
- [ ] `ReplayLogTests.cs` — append + readFrom returns entries in order

## Skills to Apply

```
.skills/storage-data-management.md   — Redis TTL, Postgres JSONB, IPFS CID policy
.skills/dotnet-devs.md               — IAsyncEnumerable, optional DI via sp.GetService
.skills/system-architect.md          — storage tier ownership boundaries
.skills/beast-development.md         — Redis hit rate > 90%, Postgres write < 30 ms p95
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — storage events via typed EnvelopePayload
- Optional Redis: ALWAYS resolve via `sp.GetService<IConnectionMultiplexer>()` — never ctor inject
- Large tensors (above threshold): transport by `TensorPersistenceRef` reference — not inline
- IPFS use is RESERVED for large artifacts and large tensor/blob payloads only

## Acceptance Gates

- [ ] `StorageRoutingPolicy.RouteFor(TensorStorageMode.Redis)` returns `StorageTier.Redis`
- [ ] `replay_log` and `artifact_registry` migrations exist in `infra/postgres/init/`
- [ ] `StorageHealthChecker` emits `STORAGE_HEALTH_DEGRADED` when Redis is unavailable
- [ ] All new tests pass: `dotnet test src/core/MLS.Core.Tests/`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Storage/` | Create storage abstractions here |
| `src/core/MLS.Core/Tensor/TensorStorageMode.cs` | Storage mode enum (reference) |
| `src/core/MLS.Core/Tensor/TensorPersistenceRef.cs` | Artifact reference type |
| `infra/postgres/init/` | SQL migration scripts |
| `src/modules/ml-runtime/MLS.MLRuntime/Program.cs` | Reference: optional Redis pattern |
| `.prompts-update/BCG_Session_10_Extended_Document.md` | Full session spec |
