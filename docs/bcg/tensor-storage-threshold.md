# Tensor Storage Threshold Policy
## BCG Platform — Session 03 Governance Document

> **Reference**: `tensor-transport-matrix.md` · `src/core/MLS.Core/Tensor/TensorStorageMode.cs` · `src/core/MLS.Core/Tensor/TensorPersistenceRef.cs`
> **Status**: ✅ Frozen for Phase 0

---

## 1. Storage Tiers

BCG tensor storage is partitioned across three tiers, each with a distinct responsibility. The choice of tier is governed by tensor size, access frequency, TTL policy, and auditability requirements.

### 1.1 Redis — Hot Cache Tier

**Responsibility**: transient, high-frequency access.

**Use for:**
- Hot tensors accessed frequently by multiple consumers
- Transient intermediate execution state
- Recent inference outputs (e.g. last N model results per trace)
- Queue-side access optimization (pre-fetched tensors waiting for dispatch)
- Short-lived stream windows (sliding buffer)

**Rules:**
- Redis keys must follow the pattern `tensor:{tensor_id}` or `tensor:hot:{trace_id}`
- TTL must be configured per lane (short-lived: seconds to minutes)
- Redis entries are cache entries, not authoritative records
- Expiry does not require a `TENSOR_STORED` event; it is a cache eviction

### 1.2 PostgreSQL — Control-Plane Tier

**Responsibility**: authoritative metadata, lineage, and audit trail.

**Use for:**
- Tensor registry metadata (id, dtype, shape, origin, contract version)
- Lineage records (`TensorLineageRecord` → `tensor_lineage` table)
- Compatibility and audit events
- Stable references to externalized payloads (IPFS CIDs)
- Control-plane persistence for replay and investigation

**Rules:**
- Every production tensor that survives past its creating context must have a Postgres registry entry
- The `tensor_lineage` table is append-only; no lineage records are ever deleted in production
- `TENSOR_LINEAGE_CREATED` envelope must be emitted before the derived tensor is dispatched
- Postgres is not used for large payload storage

### 1.3 IPFS — Large Artifact Tier

**Responsibility**: content-addressed large payload archival.

**Use for:**
- Large tensors (above the inline size threshold)
- Dataset snapshots and training archives
- Trained model artifacts (post-training reference tensors)
- Batch archives of historical execution outputs
- Model-facing large structured inputs

**Rules:**
- IPFS CIDs are content-addressed and immutable — payload must be finalized before upload
- The CID is stored in `TensorPersistenceRef.IpfsCid` and in the Postgres registry entry
- `TENSOR_STORED` envelope is emitted after successful IPFS write with the CID as `storage_key`
- Large payload validation must use `integrity.payload_hash` comparison rather than full-copy rescan

---

## 2. Threshold Principle

A configurable byte-size threshold separates inline transport from reference transport:

```
Payload size < Threshold  →  TensorTransportClass.Inline
Payload size ≥ Threshold  →  TensorTransportClass.Reference  (Redis or IPFS)
```

**Threshold configuration key**: `Tensor:InlineSizeThresholdBytes`

| Environment | Recommended Threshold |
|-------------|----------------------|
| Development | 64 KB |
| Staging | 256 KB |
| Production | 1 MB |

The principle is fixed regardless of the threshold value: **large payloads must not destabilize the fabric**.

---

## 3. Storage Routing Decision Table

| Scenario | Storage Tier | Notes |
|----------|-------------|-------|
| Small inference output (< threshold) | `Transient` or `Redis` | Cache in Redis if consumed by multiple subscribers |
| Recent indicator value | `Transient` | Lives only for current execution cycle |
| Production tensor with lineage | `Postgres` (metadata) | Registry entry + lineage record required |
| Large training artifact | `Postgres` (ref) + `IPFS` (payload) | CID stored in Postgres, payload in IPFS |
| Dataset snapshot | `IPFS` | Content-addressed, linked from Postgres |
| Stream window buffer | `Redis` | Short TTL, evicted after window expires |
| Audit trail | `Postgres` | Append-only, permanent |
| Hot tensor for multi-consumer fan-out | `Redis` | Producer writes once; consumers fetch by key |

---

## 4. Persistence Reference Record

`TensorPersistenceRef` captures the current authoritative storage location:

```csharp
// Tensor cached in Redis after inline delivery
var persistence = new TensorPersistenceRef(
    RedisKey: $"tensor:{tensor.Id}",
    PostgresRecordId: null,
    IpfsCid: null,
    StorageMode: TensorStorageMode.Redis,
    ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5));

// Large artifact in IPFS with Postgres metadata
var persistence = new TensorPersistenceRef(
    RedisKey: null,
    PostgresRecordId: registryRowId,
    IpfsCid: "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi",
    StorageMode: TensorStorageMode.Ipfs,
    ExpiresAt: null);   // IPFS content is permanent
```

---

## 5. Lineage Across Storage Relocation

When a tensor payload is relocated from one tier to another (e.g. Redis → IPFS as part of archival), a new `TensorLineageRecord` must be appended with:

```
operations: ["storage:redis→ipfs"]
persistence_relocation_note: "Archived after TTL expiry; CID: bafybei..."
```

The tensor `id` does not change. Only the `persistence` reference is updated.
