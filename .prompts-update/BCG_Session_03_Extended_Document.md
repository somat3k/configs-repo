# Session 03 Extended Document
## Universal Tensor Contract, Tensor Lineage, and Cross-Module Compatibility Constitution

## 1. Session Purpose

Session 03 defines the universal tensor contract for the BCG ecosystem. This session is the legal and technical foundation for how advanced data moves, mutates, persists, streams, composes, and remains traceable across the Block Controller network.

Session 02 elevated the Block Controller into the runtime governor. Session 03 now defines the primary governed object that the runtime must recognize, schedule, route, cache, transform, and observe: the tensor.

This session is not limited to machine learning. The tensor contract must serve:
- inference
- training
- embeddings
- scientific and mathematical compute
- code intelligence
- text generation
- graph communication uplift
- stream normalization
- block-to-block execution
- multi-module data evolution

The output of this session is a production-grade constitution for tensor identity, structure, transport, lineage, compatibility, and storage responsibility.

## 2. Strategic Mandate

The BCG platform is moving from message-led service choreography toward tensor-governed execution. The existing repo already defines the wider distributed conditions: a Block Controller orchestration hub, dynamic specialist modules, Blazor MDI operator surface, and a persistence layer spread across PostgreSQL, Redis, and IPFS fileciteturn13file0L1-L1. The system-architect skill also already defines a central orchestration topology, typed envelope discipline, and self-reporting modules in a distributed network fileciteturn17file0L1-L1.

Session 03 strengthens that model by declaring that any advanced inter-module value must be representable as a governed tensor object, even when its original source is not numeric. Text, code, serialized graphs, vectors, sparse structures, transformed event streams, and scientific matrices must all be able to enter the platform under one controlled execution model.

This is aligned with TensorFlow’s core execution logic. TensorFlow’s `tf.function` creates compiled graph-backed callables, specializes behavior based on argument types and shapes, and explicitly warns that uncontrolled retracing is a major performance concern. Stable signatures and controlled shapes are therefore part of production correctness, not just optimization fileciteturn8file0L1-L1.

## 3. Session Objectives

### Primary Objectives
- define the canonical tensor structure for BCG
- define tensor identity and traceability rules
- define tensor shape and dtype policy
- define transport class policy for tensor movement
- define storage routing rules between Redis, PostgreSQL, and IPFS
- define tensor lineage and mutation history
- define compatibility rules across modules and execution lanes
- define production QA and certification gates for tensor-compliant modules

### Secondary Objectives
- prepare the DataEvolution module for future arbitrary-source normalization
- prepare the TensorTrainer module for high-compute tensor-native pipelines
- prepare ML Runtime and runtime kernels for stable shape-aware flows
- create a future-proof basis for graph-to-tensor and stream-to-tensor composition

## 4. Session Deliverables

1. universal tensor contract
2. tensor lifecycle model
3. tensor lineage model
4. shape and dtype governance policy
5. tensor transport matrix
6. tensor storage threshold policy
7. tensor compatibility and transformation rules
8. observability requirements for tensor operations
9. QA gates for tensor-compliant modules
10. document update obligations for every tensor-aware species

## 5. Universal Tensor Doctrine

## 5.1 Foundational Definition

A BCG tensor is the canonical high-value data carrier used by the platform for governed execution. It is not merely a payload buffer. It is a traceable, typed, shape-aware execution object carrying enough metadata to support:
- routing
- validation
- caching
- persistence
- replay
- transformation
- compatibility checks
- provenance reconstruction
- performance attribution

A tensor may wrap a native runtime tensor, a flat data buffer, a sparse representation, a graph-derived structure, or an externalized large artifact reference, provided it obeys the universal contract.

## 5.2 Canonical Tensor Identity

Every tensor must carry:
- `tensor_id`
- `origin_block_id` or `origin_module_id`
- `trace_id`
- `created_at`
- `dtype`
- `shape`
- `payload_location`
- `lineage_parent_ids`
- `tags`
- `contract_version`

### Identity Rules
- tensor identity is immutable
- payload may be transformed, but a materially transformed tensor becomes a new tensor with lineage back to the prior one
- trace ID must remain stable across a request/session lineage unless a new root process is intentionally created
- origin block and origin module must be explicit for every production tensor

## 6. Canonical Tensor Structure

The canonical logical fields are:

- `id`: globally unique tensor identifier
- `dtype`: declared scalar or semantic type
- `shape`: declared dimensions, including dynamic dimensions where allowed
- `layout`: dense, sparse, ragged, sequence, graph-derived, artifact-backed
- `encoding`: binary/raw/structured/external reference format
- `data`: flat data payload when inlined
- `meta`:
  - origin block
  - origin module
  - timestamp
  - trace id
  - session id if applicable
  - tags
  - version
  - confidence or quality metadata if relevant
- `lineage`:
  - parent tensors
  - transformation steps
  - compatibility notes
- `persistence`:
  - Redis key if cached
  - Postgres record reference if persisted
  - IPFS CID if externalized
- `integrity`:
  - checksum/hash
  - schema or contract fingerprint
  - optional signature for trusted artifacts

## 7. Tensor Classes

The platform will support several governed tensor classes.

### 7.1 Scalar and Small Dense Tensors
Used for:
- control signals
- thresholds
- compact features
- simple outputs

### 7.2 Dense Numeric Tensors
Used for:
- model inference
- science and math compute
- training data slices
- dense feature matrices

### 7.3 Embedding Tensors
Used for:
- text embeddings
- image embeddings
- code embeddings
- semantic similarity search

### 7.4 Sequence Tensors
Used for:
- token sequences
- event sequences
- ordered stream windows
- temporal execution data

### 7.5 Ragged or Sparse Tensors
Used for:
- variable-length structures
- tokenized code/text segments
- graph-derived adjacency or incidence structures
- irregular scientific data

### 7.6 Artifact-Backed Tensors
Used for:
- large arrays
- large model inputs
- snapshots
- batch archives
- externalized intermediate states

### 7.7 Graph-Derived Tensors
Used for:
- graph communication uplift
- graph neighborhood projection
- multi-hop relationship matrices
- transformed topology-driven intelligence

## 8. DType Governance

## 8.1 Core Rule
DType is not only a serialization detail. It is part of execution legality.

The proto direction already includes semantic and primitive types such as float, int, bytes, json, string, and embedding-oriented categories in the user’s architecture draft. Session 03 formalizes the policy behind those classes.

### Required DType Families
- float32
- float64
- int32
- int64
- uint8
- bool
- string
- bytes
- json-semantic payloads
- embedding semantic vectors

### DType Rules
- no hidden casting in production routes
- any cast must be explicit, observable, and lineage-preserving
- lossy casts require policy approval and must be marked in lineage
- module input contracts must declare accepted dtypes
- fallback casting may exist only in the transformation bus, not as uncontrolled runtime behavior

## 8.2 Semantic DTypes
Certain payloads are semantically special even if they serialize as bytes or strings.

Examples:
- code token streams
- JSON semantic structures
- embeddings
- graph-serialized packets
- text-generation token outputs

These may share physical encoding classes with other types but must preserve semantic tags in metadata so downstream modules know the legal interpretation.

## 9. Shape Governance

## 9.1 Shape is a Runtime Constraint
TensorFlow’s execution model makes shape discipline essential because shape variance contributes to retracing and concrete-function multiplication fileciteturn8file0L1-L1. Session 03 therefore defines shape policy as part of runtime stability.

### Shape Rules
- shapes must be declared on tensor creation
- dynamic dimensions are allowed only where the module contract permits them
- uncontrolled unknown rank is forbidden in production lanes
- tensors entering compiled or batch-optimized lanes must obey static or bounded dynamic shape policy
- modules must declare shape tolerance classes:
  - exact shape
  - bounded dynamic dimension
  - ragged accepted
  - sparse accepted

## 9.2 Shape Classes
- exact-static
- bounded-dynamic
- ragged-structured
- sparse-structured
- graph-derived-variable

## 9.3 Shape Enforcement
The runtime governor may reject, reroute, or invoke transformation when:
- shape is incompatible with a target kernel
- shape exceeds a lane budget
- shape would break static-batch optimization
- shape would cause unbounded retracing risk

## 10. Tensor Lifecycle Model

### Stage 1 — Creation
Tensor is born from:
- a block kernel
- a transport adapter
- DataEvolution transformation
- TensorTrainer output
- ML Runtime inference result
- external ingestion path

### Stage 2 — Validation
Tensor is validated for:
- contract version
- dtype legality
- shape legality
- payload integrity
- origin metadata completeness

### Stage 3 — Routing Decision
Tensor is routed by the Block Controller/governor based on:
- target capability
- transport class
- health of recipient modules
- lane suitability
- size and persistence policy

### Stage 4 — Execution Use
Tensor is:
- consumed directly
- transformed
- batched
- streamed
- externalized
- cached

### Stage 5 — Persistence / Expiration
Tensor is:
- cached in Redis
- referenced in Postgres
- externalized to IPFS
- archived
- dropped after TTL expiry if transient

### Stage 6 — Lineage Continuation
If transformed, a new tensor is created with back-reference lineage.

## 11. Tensor Lineage Constitution

## 11.1 Purpose
Lineage is mandatory for:
- auditability
- replay
- debugging
- scientific reproducibility
- model validation
- stream transformation traceability
- multi-module causal reconstruction

## 11.2 Lineage Record Contents
Each tensor lineage record must include:
- parent tensor IDs
- transformation or computation step ID
- producing block/module
- producing kernel version or transform version
- timestamp
- compatibility notes
- cast/reshape/pad/slice operations if performed
- persistence relocation events if payload location changed

## 11.3 Lineage Rules
- no silent mutation without lineage
- transformed outputs become new tensors
- lineage must survive storage relocation
- replay and audit systems must be able to reconstruct the ancestry chain
- lineage must not depend only on volatile memory

## 12. Tensor Mutation Policy

### Immutable by Default
A tensor is immutable once published beyond the local producing context.

### Allowed Local Mutations
Inside a kernel or transformation path, local mutable working state may exist before publication, but once emitted:
- tensor identity is frozen
- payload is frozen
- metadata is frozen except controlled persistence annotations or telemetry attachments not affecting meaning

### Material Transformation Rule
Any reshape, cast, pad, slice, broadcast, compression change, semantic reinterpretation, or value change that affects downstream behavior creates a new tensor identity.

## 13. Tensor Transport Matrix

The system already supports HTTP, WebSocket/SignalR, and broader inter-module networking patterns, including startup registration, heartbeat, and resilient connectivity expectations fileciteturn19file0L1-L1 fileciteturn20file0L1-L1. Session 03 adds tensor routing rules on top.

### 13.1 Inline Transport
Use when:
- payload is small
- low-latency sync path is needed
- no externalization threshold is exceeded

Typical transports:
- gRPC request/response
- WebSocket event payload
- HTTP API body for management-scale payloads

### 13.2 Stream Transport
Use when:
- partial outputs are meaningful
- large series of tensors emerge over time
- live updates or long-running transforms produce incremental outputs

Typical transports:
- gRPC streaming
- SignalR/WebSocket streams
- SSE for limited compatibility flows

### 13.3 Reference Transport
Use when:
- payload is large
- multiple consumers may need the same object
- replay/archival is required
- inline transfer would create pressure or duplication

Typical pattern:
- small metadata envelope inline
- Redis/Postgres/IPFS reference attached
- optional lazy fetch by consumer

## 14. Tensor Storage Routing Policy

The repo’s storage skill already separates PostgreSQL for authoritative relational state, Redis for cache/session state, and IPFS for large distributed artifacts fileciteturn22file0L1-L1. Session 03 applies those responsibilities to tensors.

### 14.1 Redis
Use for:
- hot tensors
- transient intermediate execution state
- recent inference outputs
- queue-side access optimization
- short-lived stream windows

### 14.2 PostgreSQL
Use for:
- tensor registry metadata
- lineage records
- compatibility and audit events
- stable references
- control-plane persistence

### 14.3 IPFS
Use for:
- large tensors
- dataset snapshots
- training artifacts
- batch archives
- model-facing large structured inputs

### 14.4 Threshold Principle
A configurable size threshold must determine when tensors stop traveling inline and begin traveling by reference. The threshold must be measurable and environment-aware, but the principle is fixed: large payloads must not destabilize the fabric.

## 15. Tensor Compatibility and Transformation Rules

## 15.1 Compatibility Classes
A module must declare whether it accepts:
- exact dtype only
- exact shape only
- bounded dynamic shape
- semantic dtype family
- sparse/ragged forms
- artifact-backed tensors
- graph-derived tensors

## 15.2 Transformation Bus Responsibility
The transformation bus is the lawful place for:
- explicit cast
- reshape
- broadcast
- slice
- pad
- sparse/dense conversion
- graph-to-tensor adaptation
- semantic normalization

It is not lawful for target modules to rely on hidden coercion.

## 15.3 Failure Behavior
If a tensor is incompatible and no legal transformation exists:
- the route must fail explicitly
- the failure must emit a typed compatibility error
- lineage and telemetry must record the failure cause
- no fallback silent reinterpretation is allowed

## 16. Tensor Contracts for Special Future Modules

## 16.1 TensorTrainer
TensorTrainer must be able to ingest:
- dense numeric scientific tensors
- sequence tensors for text/code
- graph-derived tensors from transformed structures
- artifact-backed training datasets

TensorTrainer outputs must include:
- trained artifact references
- evaluation tensors
- quality metrics
- lineage back to dataset versions and transformation steps

## 16.2 DataEvolution
DataEvolution must be able to emit:
- canonical normalized tensors from arbitrary source forms
- lineage-rich transformed tensors
- graph-derived tensors for communication uplift
- semantic tensors usable by downstream runtime lanes

DataEvolution is therefore one of the strictest consumers of the universal tensor contract.

## 17. Observability Requirements

Every tensor-critical action must produce enough telemetry to reconstruct:
- where tensor came from
- who validated it
- who transformed it
- who rejected it
- how long it spent in queues or transport
- where it was cached or externalized
- which route consumed it

### Mandatory Telemetry Fields
- tensor_id
- trace_id
- origin_block_id
- producer_module
- consumer_module
- dtype
- shape summary
- storage mode
- transport mode
- validation outcome
- transformation outcome
- latency bucket

## 18. QA and Certification Gates

A module is not tensor-certified unless it passes all applicable categories.

### 18.1 Required Test Classes
- tensor contract validation tests
- dtype compatibility tests
- shape compatibility tests
- transformation correctness tests
- transport serialization tests
- storage routing tests
- lineage persistence tests
- replay and recovery tests
- performance tests for hot tensor paths

### 18.2 Certification Outcomes
A module may be certified as:
- tensor-native
- tensor-compatible via transformation bus
- tensor-read-only consumer
- tensor-producing specialist
- not certified for production tensor lanes

## 19. Performance Standards for Session 03

### Validation
- validation of ordinary tensors should remain low-latency and allocation-conscious
- large payload validation must prefer fingerprint/reference checks over repeated full-copy scans where possible

### Transformation
- transformation overhead must remain visible and budgeted
- repeated standard transforms should be cacheable or reusable where safe

### Routing
- compatibility decision must be fast enough not to turn the controller into a bottleneck
- route plus validation plus target resolution should remain in control-plane budget ranges established by earlier sessions

### Batch Fitness
- tensors entering batch lanes must expose sufficient shape guarantees to avoid destabilizing throughput optimization
- TensorFlow’s own dataset model makes batch and prefetch discipline central for high-throughput execution, especially where static shapes matter fileciteturn9file0L1-L1

## 20. Required Documents Updated by Session 03

The following living documents must be updated globally or per relevant module:
- tensor contract
- transport contract
- runtime contract
- storage policy
- lineage policy
- performance budget
- observability schema
- QA strategy
- module charter addenda for tensor-aware species

## 21. Session 03 Acceptance Criteria

Session 03 is complete only if:
- the universal tensor contract is documented
- tensor identity, dtype, and shape rules are frozen for this phase
- lineage policy is explicit
- transport policy distinguishes inline, stream, and reference movement
- storage policy distinguishes Redis, Postgres, and IPFS responsibilities
- compatibility and transformation rules are explicit
- QA classes for tensor compliance are defined
- TensorTrainer and DataEvolution future modules can operate under this contract

## 22. Session 03 Final Statement

Session 03 makes the tensor the lawful execution object of the BCG ecosystem. From this point forward, advanced values inside the fabric are not treated as anonymous payloads. They are governed, shaped, typed, traceable, persistent when necessary, transformable only through explicit law, and observable across their entire lifetime.

The platform can now move into later sessions with a stable basis for runtime kernels, batch containers, training species, transformation species, and cross-module graph intelligence without degenerating into incompatible ad hoc payload handling.
