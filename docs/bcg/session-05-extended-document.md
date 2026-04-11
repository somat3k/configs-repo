# Session 05 Extended Document
## Runtime Kernel Model and Block Execution Fabric

## 1. Session Purpose

Session 05 defines the execution constitution of the BCG platform. If Session 02 made the Block Controller the runtime governor, and Session 03 established the tensor as the advanced execution boundary, Session 05 defines the thing that actually performs governed work inside the fabric: the kernel.

This session formalizes the block execution fabric as a production-grade kernel ecosystem where each block is backed by a lifecycle-managed execution unit, every execution lane is observable and policy-aware, and no runtime path is allowed to remain ambiguous in terms of state, scheduling, streaming, cancellation, or disposal.

The objective is not to describe kernels as helper classes. The objective is to define kernels as the universal execution primitive of the system.

## 2. Strategic Position

The current project direction already supports a distributed control fabric built around a Block Controller, specialist modules, live communication channels, and a storage plane backed by Redis, PostgreSQL, and IPFS fileciteturn13file0L1-L1. The controller is already documented as the central orchestration hub responsible for registration, lifecycle management, message routing, heartbeats, global coordination, and observability aggregation fileciteturn14file0L1-L1. Session 05 extends this foundation downward into the execution plane.

The result is a new runtime statement:

> The Block Controller governs execution, but kernels perform execution. Blocks are the operator-facing composition objects; kernels are the production-facing compute objects.

## 3. Session 05 Core Decisions

### 3.1 Every executable block resolves to a kernel
A block may be visual, logical, compositional, or operator-facing, but any block that performs actual work must resolve to one or more kernels.

### 3.2 The kernel is the unit of execution certification
Certification, performance budgets, failure handling, observability, and lifecycle policy are applied at the kernel level.

### 3.3 Kernel state is explicit, never implied
No kernel is allowed to maintain silent or opaque mutable state in production paths. State must be classed, bounded, resettable, and observable.

### 3.4 Streaming kernels are first-class citizens
The execution fabric must support kernels that yield partial, progressive, or continuous outputs. Streaming is not treated as an afterthought or a side-channel.

### 3.5 The execution fabric must be policy-driven
The Block Controller and runtime scheduler must be able to govern where, how, and under what constraints kernels run.

## 4. Session 05 Goals

### Primary Goals
- define the production kernel model
- define kernel lifecycle phases
- define kernel state classes
- define the block-to-kernel resolution model
- define streaming, batching, and pipeline behavior at the kernel level
- define execution certification and QA gates for kernels

### Secondary Goals
- align kernel execution with tensor governance
- align kernel runtime with live-session and hot-update direction
- prepare the runtime for TensorTrainer and DataEvolution kernels
- formalize performance semantics for hot execution paths

## 5. Kernel Doctrine

## 5.1 Kernel Definition
A kernel is the smallest governed compute species in the BCG execution fabric. It receives typed inputs, executes under a declared contract, and emits typed outputs or typed stream events.

A production kernel must define:
- operation identity
- supported input contract
- supported output contract
- tensor compatibility profile
- initialization requirements
- state behavior
- execution mode support
- cancellation semantics
- disposal semantics
- observability schema
- performance budget

## 5.2 Kernel Categories

### Pure Kernels
- deterministic
- stateless across invocations
- same input contract yields same output contract
- suitable for aggressive scaling and replication

### Stateful Kernels
- maintain bounded state between invocations
- used for rolling windows, accumulators, sequence contexts, caches, or adaptive logic
- require reset, snapshot, and visibility rules

### Streaming Kernels
- emit partial outputs during execution
- may support long-lived or progressive result production
- must support stream termination and cancellation policy

### Transformational Kernels
- convert one representation into another
- primary use in DataEvolution, tensor reshaping, schema mapping, and graph uplift

### Composite Kernels
- orchestrate multiple child kernels under a local pipeline or execution graph
- expose a controlled outer execution contract

### Training Kernels
- govern compute-heavy training tasks
- expected in TensorTrainer and advanced ML species
- require checkpoint, progress, and artifact emission rules

## 6. Block-to-Kernel Resolution

## 6.1 Separation of concerns
Blocks exist as composition elements, graph nodes, and operator-level units. Kernels exist as execution units. One block may map to:
- one kernel
- many kernels
- a composite kernel
- a specialized dispatcher kernel

## 6.2 Resolution Pipeline
The runtime must resolve an executable block through these steps:
1. block identity lookup
2. operation type resolution
3. capability verification
4. kernel registry lookup
5. config binding
6. policy validation
7. runtime placement decision
8. initialization or reuse decision
9. execution lane assignment

## 6.3 Resolution Rules
- no block may execute without a resolved kernel identity
- no kernel may run without capability and policy validation
- no implicit kernel substitution is allowed in production unless policy explicitly permits fallback

## 7. Kernel Lifecycle Constitution

Every production kernel must support a lifecycle with explicit phase semantics.

## 7.1 Declared Phases

### Declared
Kernel type is known to the registry but not yet configured.

### Bound
Configuration and execution policy are attached.

### Initializing
Model load, dependency setup, memory preallocation, or resource binding is taking place.

### Ready
Kernel is healthy and can accept work.

### Executing
Kernel is actively computing.

### Streaming
Kernel is actively producing partial results.

### Suspended
Kernel remains instantiated but is not currently schedulable.

### Draining
Kernel accepts no new work and only completes in-flight operations.

### Failed
Kernel has crossed a failure threshold or entered an invalid execution state.

### Disposed
Kernel has released resources and is no longer valid.

## 7.2 Lifecycle Rules
- transitions must be observable
- invalid transitions are hard errors
- all failures must record context and reason
- stateful kernels must expose reset and disposal boundaries
- draining must be supported for live update paths

## 8. Kernel State Constitution

## 8.1 State Classes

### Invocation State
Exists only for one execution call.

### Session State
Survives across related executions in one governed runtime session.

### Block State
Attached to the block instance and may survive across calls.

### Kernel Internal State
Private but declared state used for rolling windows, compiled resources, tokenizers, models, or accumulators.

### Checkpoint State
Persistable state suitable for recovery or controlled resumption.

## 8.2 State Rules
- all state must be classed
- all mutable state must have reset semantics
- hidden global mutable state is forbidden in production kernels
- checkpointable kernels must define serialization rules
- state must never violate tensor lineage or traceability expectations

## 9. Execution Fabric Model

## 9.1 The Execution Fabric
The execution fabric is the runtime environment that receives block execution intent and turns it into governed kernel work. It consists of:
- kernel registry
- placement resolver
- scheduler
- queueing and backpressure system
- execution workers
- stream dispatcher
- state manager
- telemetry hooks
- failure isolator

## 9.2 Fabric Responsibilities
- resolve and prepare kernels
- schedule work into valid lanes
- preserve ordering where required
- parallelize where allowed
- stream partial results safely
- enforce budgets, timeouts, and cancellation
- record execution telemetry
- support draining, suspension, and replacement

## 10. Execution Lanes

## 10.1 Standard Lanes

### Sync Lane
Used for short-lived, deterministic work. Target for low-latency response paths.

### Async Lane
Used for non-blocking work with bounded queueing.

### Batch Lane
Used for grouped tensor tasks where throughput is more important than per-item latency.

### Pipeline Lane
Used for chained execution where output of one kernel becomes input to the next.

### Streaming Lane
Used for kernels that progressively emit results.

### Heavy Compute Lane
Used for computationally expensive training or transformation work, including TensorTrainer jobs.

## 10.2 Lane Governance
The Block Controller and runtime policy engine must be able to govern:
- which lane a kernel may use
- whether fallback lanes are allowed
- whether work may be moved to another module
- whether degraded mode must shed load

## 11. Streaming Kernel Rules

The project already recognizes streaming through SignalR/WebSocket and inference channels as a core operating pattern fileciteturn19file0L1-L1. Session 05 extends this into kernel law.

### Streaming kernel must define:
- first partial emission expectations
- partial payload contract
- end-of-stream marker
- cancellation behavior
- timeout behavior
- resumability policy if supported
- checkpoint policy if long-lived

### Streaming Guarantees
- partial outputs must be typed
- partial outputs must preserve trace and origin context
- stream close reason must be observable
- controller must be able to drain or terminate streaming kernels under policy

## 12. Performance Constitution for Kernels

The repo’s performance direction emphasizes lean execution, typed channels, pooling, measurable critical paths, and minimal allocations on hot paths fileciteturn24file0L1-L1. Session 05 turns that into kernel-specific law.

## 12.1 Hot Path Rules
- no unnecessary allocations on hot paths
- pooling required for recurrent buffers where justified
- channels must be bounded with explicit full-mode policy
- cancellation tokens must propagate to execution boundaries
- benchmarks required for critical kernel families

## 12.2 Numerical and Tensor Rules
- tensor kernels must preserve dtype and shape guarantees unless transformation is explicit
- shape instability must be isolated to controlled transformation or dynamic-shape lanes
- retracing-sensitive execution paths must be stabilized through controlled signatures and shape discipline, consistent with TensorFlow execution realities around `tf.function`, `reduce_retracing`, and compiled lanes fileciteturn8file0L1-L1
- batching and prefetch-aware execution paths must be measured and budgeted, consistent with TensorFlow dataset execution guidance fileciteturn9file0L1-L1

## 12.3 Baseline Performance Targets
- low-latency control kernels: under 5 ms p95
- standard inference kernels: under 10 ms p95 where model size and runtime permit
- standard transformation kernels: under 15 ms p95 for normal payload classes
- streaming first partial: under 250 ms p95 unless declared otherwise
- heavy compute kernels: progress events mandatory for long-running operations

## 13. Registry, Discovery, and Capability Semantics

## 13.1 Kernel Registry Responsibilities
The kernel registry must hold:
- operation type
- input/output arity
- execution modes supported
- tensor support profile
- streaming support flag
- state profile
- checkpoint support flag
- resource class
- version and compatibility notes

## 13.2 Version Discipline
- kernels must be versioned
- breaking execution contract changes require a major version transition
- operator-facing blocks may keep stable names while kernel internals version forward
- controller policies must support version-aware placement

## 14. Failure and Recovery Rules

## 14.1 Failure Classes
- initialization failure
- invalid config failure
- contract mismatch failure
- resource exhaustion failure
- timeout failure
- transport-coupled stream failure
- checkpoint failure
- disposal failure

## 14.2 Recovery Expectations
Every kernel family must declare whether it supports:
- retry in place
- retry on another worker
- fallback kernel substitution
- stream resume
- checkpoint restore
- permanent fail-fast

## 14.3 Isolation
One failing kernel must not compromise:
- unrelated kernels
- unrelated modules
- controller health
- persistent state integrity

## 15. TensorTrainer and DataEvolution Alignment

## 15.1 TensorTrainer Kernel Families
The TensorTrainer module declared in Session 01 requires a kernel ecosystem that can support:
- data preparation kernels
- training kernels
- evaluation kernels
- checkpoint kernels
- artifact export kernels
- code/text/science/math generation-related compute kernels

Training kernels must support:
- progress streaming
- cancellation
- checkpointing
- artifact registration
- resource-aware scheduling

## 15.2 DataEvolution Kernel Families
The DataEvolution module requires:
- ingestion kernels
- schema mapping kernels
- tensorization kernels
- graph uplift kernels
- repair and normalization kernels
- lineage-preserving transformation kernels

DataEvolution kernels must be especially strict about:
- deterministic transformation visibility
- lineage preservation
- shape and dtype declaration
- degradation transparency

## 16. Runtime Placement and Module Cooperation

The networking model already assumes distinct network nodes, registration, discovery, and resilient module communication fileciteturn20file0L1-L1. Session 05 extends this to execution placement.

## 16.1 Placement Criteria
Kernel execution placement may depend on:
- module capability
- health score
- resource class
- lane availability
- locality to needed tensors or artifacts
- current load
- policy restrictions
- trust and security restrictions

## 16.2 Standalone Species Rule
Every module remains a standalone species, but kernels must still be routeable through the BCG control fabric. No module may become a private island whose execution semantics cannot be governed or observed.

## 17. Observability and Telemetry Schema

Every production kernel must emit structured telemetry for:
- lifecycle transitions
- initialization time
- execution count
- execution latency
- streaming duration
- output counts and output classes
- failure counts and failure classes
- cancellation counts
- queue wait time
- resource usage where available

All telemetry must carry:
- block ID
- kernel ID
- trace ID
- module ID
- lane ID
- version

## 18. Security and Trust Boundaries

Kernels are execution units and therefore risk units. A production kernel must not:
- execute undeclared code paths under privileged context
- mutate storage outside declared channels
- bypass controller policy
- emit untyped outputs on certified lanes
- persist hidden artifacts without declared references

Heavy compute and shell-triggered kernels require heightened policy controls.

## 19. Documentation Pack Requirements for Kernel-Bearing Modules

Any module containing executable kernels must maintain, at minimum:
- kernel registry document
- execution contract document
- state model document
- performance budget document
- stream contract document if applicable
- failure matrix
- placement notes
- lifecycle and disposal notes
- benchmark references
- certification checklist

## 20. Session 05 Deliverables

1. runtime kernel constitution  
2. block-to-kernel resolution model  
3. kernel lifecycle and state doctrine  
4. execution lane matrix  
5. streaming kernel rules  
6. kernel registry requirements  
7. failure and recovery taxonomy  
8. TensorTrainer and DataEvolution kernel family alignment  
9. kernel QA and certification gates  
10. performance rules for execution hot paths  

## 21. QA and Certification Gates

A kernel or kernel family is not production-certified without:
- unit tests for known input/output behavior
- contract tests for arity, type, and tensor compatibility
- lifecycle tests for init, ready, execute, fail, drain, dispose
- cancellation tests
- timeout tests
- streaming tests if applicable
- benchmark coverage for hot paths
- observability verification
- recovery notes
- operator and runbook notes

## 22. Session 05 Acceptance Criteria

Session 05 is complete only if:
- the kernel model is formally defined
- block-to-kernel resolution is explicit
- lifecycle phases are declared and governed
- state rules are explicit
- streaming kernels are first-class in the doctrine
- execution lanes are formalized
- performance rules are set for hot paths
- TensorTrainer and DataEvolution are aligned to kernel families
- QA and certification gates are defined

## 23. Risks Identified

### Risk 1 — Block logic without execution law
Without Session 05, blocks remain UI or composition concepts without reliable runtime semantics.

### Risk 2 — Hidden state drift
Without state classification, kernels will accumulate opaque behavior that breaks repeatability and recovery.

### Risk 3 — Streaming chaos
Without streaming kernel law, partial result production will become incompatible across modules.

### Risk 4 — Performance collapse under growth
Without hot-path rules, batching, pooling, and benchmark gates, the execution fabric will degrade as species multiply.

### Risk 5 — Inconsistent training and transformation behavior
Without kernel families for TensorTrainer and DataEvolution, the most computationally important modules will evolve without runtime discipline.

## 24. Final Statement

Session 05 establishes the block execution fabric as a serious production subsystem. Kernels are now the governed execution species beneath the block graph, and every future compute path must respect their lifecycle, state rules, placement rules, streaming behavior, and performance budgets.

From this point onward, the BCG platform is not only a network of modules and messages. It is a runtime of certified kernels, routed and governed by the controller, executing against tensors and typed contracts under production-grade quality and observability standards.
