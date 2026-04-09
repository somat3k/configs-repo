# Session 10 Extended Document
## Storage and State Discipline

## 1. Session Purpose

Session 10 defines the storage constitution of the BCG ecosystem. Its purpose is to assign strict ownership, durability class, retention policy, replay behavior, and performance responsibility across Redis, PostgreSQL, and IPFS so that no tensor, artifact, control-plane record, or runtime state object exists without a governed persistence model.

This session does not treat storage as a passive backend concern. Storage is a first-class part of runtime law. Every route decision, tensor lifecycle, batch outcome, model artifact, transformation lineage record, and operational event must belong to a declared state class with explicit rules for caching, persistence, archival, replay, recovery, and deletion.

## 2. Strategic Position

The existing repo direction already defines a split between PostgreSQL as primary storage, Redis as cache and real-time state, and IPFS as distributed artifact storage fileciteturn22file0L1-L1. Session 10 refines that split into production-grade discipline for the BCG ecosystem.

This session therefore stabilizes the platform around three storage responsibilities:

1. Redis governs hot, transient, low-latency runtime state and tensor cache lanes
2. PostgreSQL governs authoritative, queryable, auditable system state
3. IPFS governs large immutable artifacts, large tensor payloads, and long-horizon reproducible objects

## 3. Session 10 Goals

### Primary Goals
- define storage ownership classes
- define what state is ephemeral, persistent, durable, replayable, or archival
- define how tensors move through cache, database, and artifact lanes
- define how artifacts and lineage records remain reproducible
- define how recovery and replay operate after faults or restarts

### Secondary Goals
- reduce ambiguity between cache and source of truth
- protect runtime performance by preventing misuse of PostgreSQL for hot-path state
- protect correctness by preventing Redis from becoming accidental authority
- protect large-object flows by routing them away from primary data paths

## 4. Session 10 Deliverables

1. storage governance constitution  
2. state class taxonomy  
3. tensor cache routing rules  
4. control-plane persistence law  
5. artifact and large object routing policy  
6. replay and recovery doctrine  
7. retention and deletion matrix  
8. storage observability model  
9. QA and certification gates for storage behavior  
10. module storage ownership checklist  

## 5. Storage Doctrine

## 5.1 Redis Doctrine

Redis is the hot-state substrate of the platform. It exists to serve:
- tensor hot cache
- module session state
- transient route and scheduling state
- stream buffers and event fan-out helpers
- locks and short-lived coordination primitives
- dead-letter holding areas for short-horizon recovery

Redis is not the final authority for:
- audited business truth
- immutable lineage history
- long-term artifact references
- certified model state
- production compliance records

### Redis Rules
- all Redis keys must be namespaced by species and state class
- all Redis content must have TTL policy unless explicitly exempted
- no critical authority may exist only in Redis
- any state required after restart must have an authoritative durable copy elsewhere
- dead-letter retention in Redis is temporary by design

## 5.2 PostgreSQL Doctrine

PostgreSQL is the authoritative relational and audit substrate of the platform. It exists to store:
- module registry and capability state
- controller decisions and operational records where audit is required
- mutation chain and lineage history
- model registry metadata
- tensor metadata where durable traceability is required
- feature and transformation schemas
- session logs and governance records
- compliance and rollback references

### PostgreSQL Rules
- PostgreSQL is the source of truth for durable control-plane state
- read-heavy hot paths must avoid unnecessary tracking and locking
- write paths must be explicit about consistency and durability expectations
- JSONB is allowed for flexible structured payloads but must remain queryable and indexed where operationally relevant
- table partitioning and archival strategies must be used for large temporal domains

## 5.3 IPFS Doctrine

IPFS is the immutable object and large-payload substrate of the platform. It exists to store:
- model artifacts
- training datasets and snapshots
- large tensors and tensor collections
- long-horizon experimental outputs
- transformation snapshots
- reproducibility bundles and audit bundles

### IPFS Rules
- any production artifact pinned for long-horizon access must be pinned deliberately
- IPFS references must always be anchored by metadata in PostgreSQL
- no runtime should depend on unresolved or untracked CIDs
- mutable runtime coordination must never be implemented directly through IPFS

## 6. State Class Taxonomy

Every stored object must belong to one of the following classes.

### Class A — Hot Ephemeral State
Examples:
- live routing hints
- short-lived tensor cache entries
- temporary queue cursors
- live session presence

Default store:
- Redis

Durability:
- non-authoritative
- restart loss acceptable unless mirrored elsewhere

### Class B — Hot Recoverable State
Examples:
- in-flight batch tracking
- resumable stream offsets
- temporary transformation progress
- model warm-up state references

Default store:
- Redis + PostgreSQL reference if recovery required

Durability:
- recoverable by design
- bounded restart survival requirements

### Class C — Authoritative Control State
Examples:
- module registry
- controller policy records
- runtime lane declarations
- schema versions
- rollout state
- route authority snapshots

Default store:
- PostgreSQL

Durability:
- authoritative
- restart loss not acceptable

### Class D — Lineage and Audit State
Examples:
- tensor provenance
- mutation history
- transformation chains
- model activation history
- operator command logs

Default store:
- PostgreSQL
- IPFS for large bundles if required

Durability:
- durable and auditable

### Class E — Artifact State
Examples:
- ONNX models
- JOBLIB files
- training snapshots
- large tensor exports
- replay bundles

Default store:
- IPFS
- PostgreSQL metadata anchor

Durability:
- immutable and reproducible

## 7. Tensor Storage Law

## 7.1 Tensor Routing Classes

### Small Tensors
- inline payload allowed
- Redis hot cache preferred for short-horizon execution
- PostgreSQL metadata only when traceability needed
- no IPFS routing unless bundled in archival package

### Medium Tensors
- inline or externalized depending on lane budget
- Redis only if hot-path reuse is likely and TTL is bounded
- PostgreSQL stores metadata, hashes, and lineage
- IPFS optional if reproducibility or archival is required

### Large Tensors
- externalized by default
- IPFS preferred for immutable large objects
- Redis may store only references, not full payloads
- PostgreSQL stores authoritative reference metadata and lineage

## 7.2 Tensor Authority Rules
- Redis may accelerate tensor access but may not redefine tensor meaning
- PostgreSQL governs tensor identity, provenance, and audit references when durability is required
- IPFS governs immutable payload preservation for large or archival tensors
- tensor deletion must preserve lineage legality; metadata may remain after payload retirement if audit obligations exist

## 8. Artifact Constitution

Artifacts include:
- trained models
- feature bundles
- tokenizer assets
- scientific or mathematical corpora snapshots
- transformation bundles
- replay packs
- benchmark and certification payloads

### Artifact Rules
- every artifact must have a manifest
- every artifact manifest must include checksum and version
- every artifact must declare compatibility against consuming modules
- every artifact must declare retention horizon
- every artifact must have a rollback relation if promoted to production use

## 9. Replay and Recovery Doctrine

## 9.1 Replay Classes

### Operational Replay
Reconstructs recent runtime decisions and state transitions after service interruption.

Backing stores:
- PostgreSQL operational tables
- Redis dead-letter or short-horizon buffers
- optional IPFS replay packs for long-range events

### Tensor Replay
Reconstructs tensor lineage and transformation chain.

Backing stores:
- PostgreSQL metadata and lineage
- IPFS large tensor snapshots when required

### Model Replay
Reconstructs serving state for previously active models.

Backing stores:
- PostgreSQL registry and activation records
- IPFS artifact payloads

## 9.2 Recovery Rules
- every recoverable workflow must define whether it supports resume, restart, or replay-only restoration
- partial recovery must not silently duplicate side effects
- failed resumptions must become visible operational events
- controller-visible state must converge after recovery, not drift

## 10. Retention and Deletion Policy

## 10.1 Redis Retention
- always bounded
- class-based TTL
- extension only when justified by lane policy
- no infinite growth on stream or cache keys

## 10.2 PostgreSQL Retention
- control-plane records retained according to operational and audit needs
- high-volume telemetry partitioned and archived
- transformation and tensor lineage retained according to reproducibility policy
- deletion must preserve referential legality

## 10.3 IPFS Retention
- production artifacts pinned until retirement and grace window expiry
- retired artifacts may be unpinned only after manifest, audit, and rollback windows are satisfied
- experimental artifacts must still declare retention intent

## 11. Storage Ownership by Major Species

## 11.1 Block Controller
Owns in PostgreSQL:
- module registry authority
- route history where audited
- policy state
- rollout records

Uses Redis for:
- short-horizon route hints
- live connection/session helpers
- ephemeral coordination

## 11.2 DataEvolution
Owns in PostgreSQL:
- source schemas
- mapping definitions
- transformation lineage
- drift and repair records

Uses Redis for:
- active transformation working state
- short-horizon source offsets
- temporary stream buffers

Uses IPFS for:
- large snapshots
- evolution bundles
- long-horizon replay packages

## 11.3 TensorTrainer
Owns in PostgreSQL:
- job metadata
- run manifests
- validation summaries
- promotion readiness

Uses Redis for:
- live progress and bounded training session state

Uses IPFS for:
- datasets
- checkpoints
- exported artifacts
- reproducibility bundles

## 11.4 ML Runtime
Owns in PostgreSQL:
- model activation metadata
- signature registry
- rollout and rollback history
- confidence drift references

Uses Redis for:
- hot model state helpers
- hot tensor cache
- short-horizon session routing

Uses IPFS for:
- promoted artifacts
- rollback payloads
- large evaluation bundles

## 12. Performance Standards

### Redis
- cache lookup must remain a hot-path operation
- avoid oversized value storage where references would suffice
- lock usage must remain bounded and observable

### PostgreSQL
- no uncontrolled chatty writes on hot execution paths
- authoritative writes must be shaped for predictable latency
- large historical tables require partitioning and archival discipline

### IPFS
- not allowed on synchronous ultra-low-latency hot paths unless pre-fetched or locally pinned
- artifact fetch must be instrumented and budgeted
- runtime should prefer resolved local access for activated production artifacts

## 13. Observability and Telemetry

Every storage interaction of consequence must emit enough metadata to answer:
- what was read or written
- which module did it
- which trace or tensor lineage it belonged to
- whether the object was cache, authority, or artifact class
- whether the action was hot-path, control-plane, or archival
- whether the action succeeded, retried, degraded, or failed

### Required Metrics
- Redis hit rate by class
- Redis memory pressure and eviction rate
- PostgreSQL write latency and query latency by table family
- IPFS fetch latency, pin latency, and resolution success rate
- replay success rate
- artifact activation fetch duration
- dead-letter queue depth
- lineage persistence lag

## 14. Failure Model

### Redis Failures
Expected consequences:
- hot-path slowdown
- loss of ephemeral state
- increased PostgreSQL load if fallback exists

Required response:
- degrade, do not corrupt authority
- raise cache-loss event
- preserve operational continuity where possible

### PostgreSQL Failures
Expected consequences:
- control-plane authority unavailable
- rollout state and audit paths impaired
- durable writes blocked

Required response:
- safe degradation
- promotion freeze
- explicit operator-visible failure state

### IPFS Failures
Expected consequences:
- artifact fetch delays
- inability to restore large immutable objects
- degraded replay capability

Required response:
- fallback to already pinned or pre-fetched copies if available
- block unsafe promotions
- mark artifact restoration as unavailable

## 15. QA and Certification Gates

No module may be certified against Session 10 without:

1. declared storage ownership map  
2. explicit state classes for its major objects  
3. TTL and retention definitions  
4. replay and recovery notes  
5. observability coverage for critical storage paths  
6. resilience tests covering at least one store outage  
7. proof that Redis is not misused as durable authority  
8. proof that PostgreSQL is not misused as a hot-path blob store  
9. proof that IPFS references are anchored by metadata  

## 16. Acceptance Criteria

Session 10 is complete only if:
- Redis, PostgreSQL, and IPFS responsibilities are explicit
- every major BCG species can map its states into the taxonomy
- replay and recovery classes are defined
- retention and deletion rules exist
- large tensors and artifacts have routing law
- observability and QA expectations are explicit
- no major ambiguous storage boundary remains

## 17. Session 10 Final Statement

Session 10 turns storage from infrastructure into law. Redis accelerates but does not rule. PostgreSQL governs authority and audit. IPFS preserves the immutable and the large. Together they form a disciplined storage triad for BCG: fast where runtime must be fast, durable where truth must be durable, and reproducible where future evolution must remain explainable.
