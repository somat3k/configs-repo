# Session 09 Extended Document
## ML Runtime Refactor into Hybrid Tensor Service

## 1. Session Purpose

Session 09 defines the production refactor of **ML Runtime** into a **hybrid tensor service** that sits between training, transformation, artifact governance, and live inference. This session does not describe ML Runtime as a generic model host. It establishes ML Runtime as a governed runtime species in the BCG ecosystem with strict tensor contracts, certified artifact activation, runtime-safe model switching, and performance-aware serving lanes.

The intent of this session is to unify four domains that are often left fragmented:

1. model intake and artifact validation  
2. tensor-native inference and streaming  
3. compatibility with upstream transformation systems  
4. controlled interaction with training and promotion systems  

From this point forward, ML Runtime is not allowed to operate as a loose inference wrapper. It becomes a formal **hybrid tensor service** under BCG governance.

---

## 2. Strategic Position in the BCG Ecosystem

The current repository direction already establishes `ml-runtime` as a hybrid service: Python handles training and export, while C# serves production inference using ONNX-compatible runtime behavior and WebSocket/HTTP delivery patterns. The existing project guidance explicitly frames ML Runtime as a hybrid C#/Python service and sets low-latency inference targets and model export rules around ONNX and JOBLIB. Session 09 formalizes that direction into production operating law.

Within the BCG fabric, ML Runtime occupies the following strategic position:

- **Downstream of DataEvolution** for normalized, lineage-preserving, tensor-ready inputs
- **Downstream of TensorTrainer** for certified and promotable model artifacts
- **Under the Block Controller** for routing, capability resolution, health status, and execution lane assignment
- **Upstream of specialist modules** that require scoring, prediction, confidence estimation, ranking, or semantic outputs
- **Parallel to direct execution kernels** when a decision must be served from a certified model rather than computed from procedural logic

ML Runtime therefore becomes the primary **model-serving species** of the BCG system.

---

## 3. Session 09 Core Decision Set

### 3.1 ML Runtime is a hybrid tensor service
It must support both:
- artifact-aware serving of compiled and certified model forms
- tensor-native request and response handling

### 3.2 Artifact intake and serving are separated
Training, experimentation, and model creation may occur elsewhere. ML Runtime only accepts artifacts that pass intake governance.

### 3.3 Serving must be signature-bound
No runtime activation is valid without explicit input and output contracts, including tensor shapes, dtypes, semantic field mapping, and lineage policy.

### 3.4 Runtime activation must be reversible
Model rollout, shadow mode, canary mode, and rollback are mandatory operating capabilities.

### 3.5 Inference is not one lane
ML Runtime must support multiple execution lanes:
- synchronous low-latency inference
- streaming inference
- batch inference
- shadow evaluation
- replay and validation lanes
- model comparison and A/B lanes

### 3.6 Confidence is a first-class output
Where supported by model family, confidence must be explicitly surfaced, logged, and policy-addressable.

---

## 4. Session Goals

### Primary Goals
- refactor ML Runtime into a strict hybrid tensor species
- define serving lanes and activation governance
- define artifact intake, validation, and promotion boundaries
- define runtime compatibility with DataEvolution and TensorTrainer
- define observability, rollback, and production QA requirements

### Secondary Goals
- reduce coupling between training and serving
- improve stability of runtime-serving contracts
- support high-performance model switching without service instability
- establish model lifecycle governance across dev, stage, and prod

---

## 5. Bounded Responsibility

ML Runtime is responsible for:
- accepting certified model artifacts and metadata
- loading model artifacts into production-serving lanes
- exposing tensor-native prediction APIs and streams
- enforcing signature compatibility for active models
- recording inference metrics and runtime health
- supporting comparison, canary, shadow, and rollback modes
- serving confidence, scoring, embedding, ranking, or generation outputs when the active model type allows it

ML Runtime is **not** responsible for:
- arbitrary raw data normalization without DataEvolution governance
- uncontrolled experimental training loops in production-serving nodes
- bypassing artifact certification or promotion rules
- accepting untyped or schema-unknown requests into certified serving lanes
- silently mutating feature contracts

---

## 6. Hybrid Runtime Architecture

## 6.1 Service Planes

### Plane A — Control Plane
Responsible for:
- artifact registration
- activation and deactivation commands
- routeability flags
- health state
- rollout mode
- telemetry and audit

### Plane B — Tensor Serving Plane
Responsible for:
- synchronous inference
- streaming results
- batch scoring
- tensor pre/post-shaping
- confidence and structured output return

### Plane C — Validation Plane
Responsible for:
- artifact verification
- contract compatibility tests
- replay validation
- shadow execution
- benchmark acceptance

### Plane D — Runtime Storage Plane
Responsible for:
- hot model metadata in Redis
- authoritative activation state in PostgreSQL
- artifact references and immutable binaries via IPFS or artifact storage

---

## 6.2 Internal Runtime Components

1. **Model Intake Manager**  
   Validates artifact manifests, signatures, model family metadata, and compatibility requirements.

2. **Artifact Loader**  
   Loads ONNX or other certified runtime-compatible forms into active or standby runtime contexts.

3. **Signature Resolver**  
   Enforces strict mapping between request tensors and model input specifications.

4. **Tensor Serving Engine**  
   Executes low-latency or batch inference with appropriate threading and batching discipline.

5. **Comparison Engine**  
   Runs A/B, shadow, replay, and canary evaluation flows without contaminating primary result lanes.

6. **Activation Controller**  
   Performs safe activation, demotion, deactivation, and rollback of model versions.

7. **Telemetry Publisher**  
   Emits health, timing, confidence, drift, and per-model runtime events.

---

## 7. Runtime Species Classes

ML Runtime must support these runtime species classes.

### 7.1 Low-Latency Scoring Runtime
Used for:
- classification
- ranking
- signal scoring
- probability and confidence outputs

Characteristics:
- synchronous or micro-batch mode
- strict latency budget
- preloaded active model
- minimal tensor transformation in-lane

### 7.2 Streaming Output Runtime
Used for:
- progressive inference
- token or chunk streaming
- long-running generation or multi-stage emission
- partial result delivery

Characteristics:
- backpressure-aware
- resumability policy defined
- partial result states observable

### 7.3 Batch Evaluation Runtime
Used for:
- scheduled scoring jobs
- replay validation
- high-throughput model comparisons
- offline or semi-online processing

Characteristics:
- queue-governed
- larger tensor containers
- stronger throughput optimization

### 7.4 Shadow Runtime
Used for:
- serving new model versions invisibly alongside active production models
- collecting comparison metrics without replacing production outputs

### 7.5 Validation Runtime
Used for:
- contract verification
- replay testing
- pre-promotion benchmark validation

---

## 8. Integration With Other Session Species

## 8.1 Integration With Block Controller
ML Runtime must register with the Block Controller as a routable species. Controller-facing declarations must include:
- module identity
- active runtime classes
- supported model families
- supported input contracts
- health state
- current activation map
- degraded mode state

The Block Controller may route traffic to ML Runtime based on:
- supported op or model family
- current load
- health score
- rollout mode
- latency SLO compliance
- regional or environment policy

## 8.2 Integration With DataEvolution
DataEvolution is the only approved upstream normalizer for non-canonical inputs entering serious model-serving lanes. ML Runtime must assume that:
- raw source diversity is handled upstream
- lineage tags are already attached
- schema drift has already been classified
- tensorization rules have already been applied

ML Runtime may still perform final tensor shaping, but it must not become a hidden universal adapter.

## 8.3 Integration With TensorTrainer
TensorTrainer is the training species. ML Runtime is the serving species. The boundary between them must be explicit.

TensorTrainer hands off:
- certified artifacts
- manifest metadata
- input/output contract definitions
- benchmark results
- compatibility notes
- intended rollout channel

ML Runtime accepts only artifacts that satisfy intake governance and are compatible with runtime-serving lanes.

---

## 9. Artifact Constitution

## 9.1 Artifact Forms
ML Runtime must understand artifact forms as governed classes.

### Primary Runtime Form
- ONNX or other explicitly approved production-serving format

### Secondary Fallback Form
- JOBLIB or equivalent validation form for restricted fallback or audit workflows

### Metadata Forms
- manifest document
- schema contract
- feature contract
- model lineage record
- benchmark record
- promotion certificate

## 9.2 Artifact Manifest Requirements
Every model artifact must ship with:
- artifact ID
- model family and task type
- version and semantic status
- producer species and training lineage
- input tensor signatures
- output tensor signatures
- expected feature schema version
- confidence support flag
- approved runtime class list
- benchmark record
- rollback predecessor
- integrity hash

## 9.3 Artifact Intake Policy
Artifacts are rejected if:
- signature information is missing
- required feature schema is unknown
- confidence head is promised but absent
- benchmark acceptance failed
- artifact integrity verification fails
- runtime compatibility notes are missing
- rollback path is undefined for production promotion

---

## 10. Signature and Tensor Contract Governance

## 10.1 Input Signature Law
Every active model must declare:
- tensor count
- tensor names
- tensor order
- dtype
- shape policy
- dynamic dimension allowances
- semantic field mapping

No active model may rely on implicit positional ambiguity.

## 10.2 Output Signature Law
Every active model must declare output classes, including where relevant:
- primary prediction
- confidence
- class probabilities
- embeddings
- auxiliary diagnostic outputs
- generation tokens or chunks

## 10.3 Shape Discipline
Stable production lanes require shape discipline. Dynamic shapes are allowed only when:
- the runtime family supports them safely
- the lane is certified for dynamic usage
- the signature contract declares the dynamic dimensions explicitly
- performance expectations are documented

## 10.4 Lineage Preservation
Every inference result must carry:
- input trace ID
- active model version
- artifact ID
- feature/schema version
- inference timestamp
- runtime lane ID
- confidence metadata when applicable

---

## 11. Serving Lanes

## 11.1 Synchronous Lane
Use when:
- end-user or module call requires direct response
- latency target is strict
- result size is bounded

Requirements:
- preloaded model
- no uncontrolled transformation steps
- no slow external dependency in hot path
- bounded timeout and failure taxonomy

## 11.2 Streaming Lane
Use when:
- output is progressive or large
- model can emit partials
- user experience benefits from continuous delivery

Requirements:
- chunk or token schema
- partial-state tracking
- cancellation support
- backpressure policy

## 11.3 Batch Lane
Use when:
- throughput dominates latency
- replay or scheduled jobs are running
- score sets are large

Requirements:
- scheduler integration
- bounded containers
- queue and fairness policy
- failure partitioning between items and batch-level state

## 11.4 Shadow Lane
Use when:
- a candidate artifact is evaluated against production traffic without altering user-visible output

Requirements:
- identical input capture
- result correlation
- shadow-only telemetry separation
- no contamination of primary output lane

## 11.5 Canary Lane
Use when:
- a small percentage of live production calls are intentionally served by the candidate model

Requirements:
- percentage-based routing
- rapid rollback switch
- comparative telemetry
- alert thresholds

---

## 12. Activation Governance

## 12.1 Activation States
A model in ML Runtime must exist in one of these states:
- registered
- validated
- staged
- active
- shadow
- canary
- draining
- suspended
- rolled_back
- retired

## 12.2 Safe Activation Rules
Activation requires:
- validated artifact
- successful load on target runtime
- successful contract test
- rollback predecessor known
- telemetry channel operational
- Block Controller aware of activation state

## 12.3 Rollback Rules
Rollback must be possible without redeploying the whole fabric. Required controls:
- active predecessor retained until promotion stabilizes
- hot reload or standby activation path
- rollback trigger thresholds
- operator and automated rollback conditions

## 12.4 Draining Rules
When a model version is being replaced:
- no abrupt termination of in-flight batch jobs
- streaming lanes may finish or be migrated depending on policy
- new requests stop entering draining version once switchover begins

---

## 13. Runtime Performance Policy

## 13.1 CPU-First Operating Assumption
ML Runtime must support strong CPU operation as a first-class scenario. Acceleration awareness is allowed, but certified CPU lanes are mandatory.

## 13.2 Runtime Optimization Classes
### L1
Single-thread vectorized and low-overhead inference for micro-latency workloads.

### L2
Multi-thread CPU parallelism for higher throughput on one machine.

### L3
Multi-process scale-out for replicated inference services or replay workloads.

### L4
Hardware acceleration where environment and artifact form allow.

## 13.3 Performance Budgets
Each deployed model must declare:
- p50, p95, p99 latency
- throughput target
- memory footprint
- model load time
- cold activation time
- batch efficiency profile

## 13.4 Hot Path Restrictions
The serving hot path must avoid:
- blocking remote calls
- uncontrolled allocations
- silent JSON reshaping for canonical tensor requests
- on-demand model reload in low-latency lanes
- runtime schema guessing

---

## 14. Storage and State Rules

## 14.1 Redis Responsibilities
- active model routing hints
- hot runtime state
- short-lived counters
- active rollout percentage markers
- ephemeral session and queue state

## 14.2 PostgreSQL Responsibilities
- authoritative model registry
- artifact manifests
- activation history
- promotion certificates
- rollback lineage
- inference metrics summaries

## 14.3 IPFS or Artifact Storage Responsibilities
- immutable artifact binaries
- large metadata bundles
- benchmark payload archives
- replay bundles and snapshot references

---

## 15. Observability Requirements

ML Runtime must expose:
- model load success/failure
- per-model latency
- per-lane throughput
- confidence distributions
- drift indicators
- batch outcomes
- shadow/canary comparison metrics
- rollback events
- artifact activation history
- feature/schema mismatch events

### Required Telemetry Dimensions
- model family
- artifact ID
- model version
- runtime lane
- request route source
- controller route decision ID
- schema version
- trace ID
- environment

---

## 16. Quality Assurance and Certification

## 16.1 Required Test Classes
- unit tests for signature resolver and artifact loader
- integration tests for runtime activation
- contract tests for input/output tensor schemas
- performance tests for serving lanes
- shadow/canary correctness tests
- rollback rehearsal tests
- replay validation tests
- degraded mode tests

## 16.2 Promotion Gates
No model may become active in production without:
- contract test pass
- benchmark pass
- telemetry validation
- artifact integrity pass
- rollback target defined
- canary or shadow policy defined

## 16.3 Runtime Certification Levels
### Bronze
Validation/runtime testable only, not production active.

### Silver
Production-capable for low-risk lanes with manual rollback.

### Gold
Production-certified with canary, shadow, telemetry, and replay support.

### Platinum
Mission-critical lane certified with rollback automation, drift watch, and hard SLO enforcement.

---

## 17. Failure Model

### Failure Class A — Artifact failure
Manifest invalid, binary corrupt, or load error.

### Failure Class B — Contract failure
Feature schema mismatch, tensor incompatibility, or output signature inconsistency.

### Failure Class C — Runtime degradation
Latency breach, memory pressure, queue inflation, or concurrency instability.

### Failure Class D — Comparison divergence
Shadow or canary outputs diverge beyond policy thresholds.

### Failure Class E — Observability failure
Metrics pipeline missing, no trace propagation, or rollback audit unavailable.

Every failure class must map to:
- detection rule
- impact scope
- operator guidance
- automated action where defined

---

## 18. Session 09 Deliverables

1. ML Runtime hybrid species charter  
2. artifact constitution and intake rules  
3. activation and rollback model  
4. serving lane policy  
5. signature governance note  
6. runtime observability requirements  
7. QA and certification matrix  
8. Block Controller integration note  
9. TensorTrainer handoff contract  
10. DataEvolution intake boundary note  

---

## 19. Session 09 Acceptance Criteria

Session 09 is complete only if:
- ML Runtime is formally defined as a hybrid tensor service
- training and serving boundaries are explicit
- artifact intake and activation rules are explicit
- rollback and canary/shadow modes are explicit
- signature and tensor contracts are explicit
- serving lanes are defined and measurable
- observability requirements exist
- QA and certification gates exist
- integration with Block Controller, DataEvolution, and TensorTrainer is explicit

---

## 20. Session 09 Final Statement

Session 09 is the point where ML Runtime stops being described as a useful module and starts being governed as the **official model-serving species** of the BCG ecosystem. It is the runtime constitution that separates training from serving without disconnecting them, preserves model lineage without freezing evolution, and allows the platform to activate, compare, roll back, and observe models as first-class controlled runtime entities.

From this point onward, every model that wishes to serve live BCG traffic must obey the ML Runtime constitution: certified artifact intake, explicit tensor signatures, measurable serving lanes, rollback readiness, and auditable activation under Block Controller governance.
