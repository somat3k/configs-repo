# Session 06 Extended Document
## Batch Containers, Scheduler Performance Lanes, and Throughput Governance

## 1. Session Purpose

Session 06 formalizes the production model for batch execution, queue control, scheduling strategies, backpressure, and performance lanes inside the BCG ecosystem. Its purpose is to move the platform beyond isolated request execution and into managed tensor flow orchestration where batches, pipelines, and controlled concurrency become first-class operating constructs.

This session establishes that throughput is not an accidental outcome of fast modules. Throughput is a governed property of the full runtime fabric, including:
- intake behavior
- task shaping
- queue discipline
- scheduler fairness
- cancellation and timeout behavior
- cache interaction
- transport pressure
- tensor shape stability
- kernel execution lanes
- observability and recovery

Session 06 is therefore the constitutional document for all high-volume prediction, transformation, and compute operations.

## 2. Strategic Position

The current repo direction already supports a distributed module platform with a central Block Controller, dedicated module ports, streaming/event patterns, Redis/PostgreSQL/IPFS storage roles, and a C#/Python hybrid ML runtime direction fileciteturn13file0L1-L1 fileciteturn15file0L1-L1. Earlier sessions in this program established:
- Session 01: governance baseline and tensor-first mandate
- Session 02: Block Controller as runtime governor
- Session 03: universal tensor contract and lineage rules
- Session 04: protobuf, envelope, and transport law
- Session 05: kernels as the universal execution primitive

Session 06 now defines how the runtime fabric actually moves work at scale.

The execution transition is from:
- one request → one module → one response

toward:
- one intake → one shaped workload → one or more batch containers → governed execution lanes → observable multi-result flow

## 3. Session 06 Core Decisions

### 3.1 Batch containers are first-class runtime objects
A batch is not merely an optimization. It is a governed runtime object that carries grouped work through explicit scheduling, execution, timeout, and telemetry rules.

### 3.2 The scheduler is a policy engine
The scheduler is not a background convenience. It is the enforcement layer for fairness, concurrency, latency control, and queue survival.

### 3.3 Performance lanes are explicit
The runtime must classify work into lanes rather than letting all workloads contend equally. Typical lanes include:
- ultra-low-latency control lane
- synchronous prediction lane
- streaming lane
- transformation lane
- training lane
- large artifact / heavy compute lane
- maintenance / replay lane

### 3.4 Backpressure is mandatory
No lane may run without declared backpressure behavior. Every channel, queue, and stream must define how it behaves under pressure.

### 3.5 Cancellation and timeout semantics must be deterministic
Every queued job, running batch, and chained workload must have clear stop rules.

## 4. Session 06 Goals

### Primary Goals
- define the batch container model
- define scheduler responsibilities and strategy law
- define performance lanes and admission control
- define backpressure and queue protection rules
- define cancellation, timeout, and retry semantics
- define observability and recovery for batch flows

### Secondary Goals
- support TensorTrainer and DataEvolution heavy workloads
- preserve low-latency paths for controller and operator actions
- reduce uncontrolled contention between modules
- create the basis for capacity planning and hardening

## 5. Session 06 Deliverables

1. batch container constitution  
2. scheduler authority model  
3. execution lane taxonomy  
4. queue and channel governance rules  
5. backpressure and overload policy  
6. timeout and cancellation policy  
7. fairness and starvation-prevention policy  
8. batch observability matrix  
9. QA and benchmark gate definitions  
10. module impact notes for all affected species  

## 6. Batch Container Doctrine

## 6.1 Definition

A batch container is the canonical runtime object for grouped task execution. It contains one or more execution units and is governed as a tracked, schedulable entity.

A batch container must have:
- batch ID
- trace/session association
- owner module or initiating authority
- strategy class
- task list
- priority / lane
- cancellation token lineage
- timing budget
- retry policy reference
- telemetry hooks
- lineage reference to source tensors or source requests

## 6.2 Why batches are mandatory

Batch containers exist to unify and control:
- grouped inference requests
- grouped transformations
- sequential tensor pipelines
- fan-out compute operations
- multi-block composed execution
- transformation chains from DataEvolution
- TensorTrainer preprocessing and mini-batch logic
- replay, warmup, and shadow execution

TensorFlow’s own dataset and batching model reinforces that performance comes from controlled grouping, prefetching, and stable shapes rather than from random request-by-request execution. Its dataset layer explicitly treats batching and prefetching as core throughput controls and highlights the importance of static shape discipline for high-performance compiled execution lanes fileciteturn9file0L1-L1.

## 6.3 Batch classes

### Class A — Micro Batches
- 2 to 32 requests or task units
- used for low-latency prediction and lightweight transforms
- optimized for minimal coordination overhead

### Class B — Standard Batches
- 32 to 512 units depending on workload
- used for normal inference, transformations, and stream normalization
- default class for controlled throughput lanes

### Class C — Heavy Batches
- large transformation or training workload slices
- may consume substantial CPU and memory
- admission-controlled and often delegated to heavy compute lanes

### Class D — Pipeline Batches
- tasks are chained sequentially or in stages
- output of stage N becomes input of stage N+1
- used for multi-step transforms, inference chains, and graph projection

### Class E — Shadow / Validation Batches
- used for canary, shadow, or A/B runtime checks
- results may be non-authoritative
- never allowed to starve authoritative production lanes

## 7. Scheduler Authority Model

The scheduler is a governed subsystem under the Block Controller runtime constitution.

It must control:
- admission of new work
- lane assignment
- queue placement
- concurrency ceilings
- fairness between tenants or modules
- timeout enforcement
- overload response
- retry sequencing
- drain and shutdown behavior

The scheduler must not be reduced to a simple worker loop. It is the authoritative system component that determines how work is allowed to consume runtime capacity.

## 7.1 Scheduler responsibilities

### Intake Shaping
Converts incoming requests into schedulable units. This includes:
- route-to-lane decisions
- grouping compatible requests into batch containers
- splitting oversized workloads
- rejecting malformed or uncertified work

### Queue Governance
Places work into the appropriate bounded structure.

### Execution Dispatch
Selects tasks based on policy rather than arrival time alone.

### Runtime Safety
Enforces timeout, cancellation, overload, and drain policies.

### Telemetry and Audit
Emits structured events for queue depth, latency, rejections, and scheduler decisions.

## 8. Execution Lane Taxonomy

Every workload must be assigned to a lane.

## 8.1 L0 — Control Lane
Purpose:
- controller commands
- health traffic
- registration/heartbeat
- routing updates
- operator control actions

Rules:
- minimal queueing
- extremely small payloads
- highest scheduling priority
- no heavy tensor work allowed

## 8.2 L1 — Low-Latency Prediction Lane
Purpose:
- synchronous inference
- lightweight tensor transformations
- operator-triggered immediate results

Rules:
- small micro-batches allowed
- strict latency budget
- static-shape preferred
- no long-running kernels

## 8.3 L2 — Standard Throughput Lane
Purpose:
- regular inference
- moderate transformations
- standard module batch operations

Rules:
- normal batching
- fairness enforced
- cancellation honored at stage boundaries

## 8.4 L3 — Streaming Lane
Purpose:
- live streams
- partial result emission
- event-driven tensor or graph updates

Rules:
- bounded subscriber pressure
- flow control mandatory
- partial result semantics required

## 8.5 L4 — Transformation Lane
Purpose:
- DataEvolution normalization
- schema mapping
- graph uplift
- source-to-tensor conversion

Rules:
- may use pipeline batches heavily
- must preserve lineage
- must emit transform telemetry and repair events

## 8.6 L5 — Heavy Compute Lane
Purpose:
- TensorTrainer workloads
- large embedding generation
- science/math/code/text heavy operations
- artifact generation and large tensor materialization

Rules:
- strict admission control
- protected CPU/memory quotas
- never allowed to dominate control or low-latency lanes

## 8.7 L6 — Replay, Warmup, and Maintenance Lane
Purpose:
- replays
- cache warmup
- preloads
- validation sweeps
- migration or maintenance batch jobs

Rules:
- lowest priority unless explicitly promoted
- suspendable during pressure
- isolated from interactive operator paths

## 9. Scheduling Strategies

## 9.1 Sequential Strategy
Use when:
- strict ordering is required
- stateful kernels require deterministic sequence
- pipeline semantics depend on stable stage ordering

Risks:
- lower throughput
- longer tail latency when one stage stalls

## 9.2 Parallel Strategy
Use when:
- tasks are independent
- kernels are stateless or isolated per unit
- contention costs are acceptable

Risks:
- memory spikes
- scheduler unfairness without bounded concurrency

## 9.3 Pipeline Strategy
Use when:
- tasks form a staged transformation
- each stage changes the tensor structure or result state
- DataEvolution or multi-kernel chains are involved

Risks:
- downstream stalls propagate upstream
- cancellation complexity increases

## 9.4 Adaptive Strategy
Use when:
- scheduler can profile queue depth, shape compatibility, and resource pressure
- workload can be grouped or split safely

Adaptive policy must never be opaque. Any automatic strategy shift must be observable and explainable.

## 10. Queue and Channel Governance

The project’s performance rules already require Channels for producer/consumer scenarios and bounded behavior on critical paths fileciteturn23file0L1-L1 fileciteturn24file0L1-L1. Session 06 formalizes that requirement.

## 10.1 Queue doctrine

Every queue must declare:
- owner
- input type
- lane
- bounded capacity
- full-mode policy
- retention semantics
- telemetry fields
- failure handling

## 10.2 Full-mode policies

Valid queue policies include:
- wait
- drop newest
- drop oldest
- reject and signal caller
- dead-letter and continue

There is no globally correct default. Each lane must choose explicitly.

### Recommended defaults
- L0 control lane: reject when saturated, never silently delay forever
- L1 low-latency lane: small bounded queue, reject or drop-oldest depending on business rule
- L2 standard lane: wait or bounded wait with timeout
- L3 streaming lane: bounded subscriber buffers with controlled drop strategy
- L4 transformation lane: wait with lane-level cap
- L5 heavy lane: strict admission queue with reject-on-pressure or deferred scheduling
- L6 maintenance lane: preemptible wait

## 10.3 Dead-letter handling

Failed deliveries or unschedulable units must have a dead-letter path with:
- reason code
- payload reference
- lineage reference
- retry eligibility
- expiration policy

Redis may be used for short-lived dead-letter routing and replay coordination, while PostgreSQL holds durable audit records and IPFS may hold large payload references when necessary fileciteturn22file0L1-L1.

## 11. Backpressure Constitution

Backpressure is not an implementation detail. It is the survival mechanism of the fabric.

Every producer and consumer pair must define:
- who slows down first
- who drops first
- how overload is signaled
- whether partial work can be preserved
- what telemetry proves the behavior is working

## 11.1 Backpressure rules

1. No unbounded in-memory growth on production lanes
2. No hidden queue expansion under pressure
3. No batch enlargement beyond declared ceilings
4. No streaming subscriber may force total fabric slowdown
5. Low-latency lanes must degrade predictably rather than silently stall

## 11.2 Overload response classes

### Soft Pressure
- queue depth rising
- batch sizes trimmed
- optional adaptive batching disabled
- maintenance lanes slowed

### Hard Pressure
- new heavy jobs rejected
- low-priority lanes throttled
- shadow and replay work paused
- transformation lane concurrency reduced

### Critical Pressure
- only control and essential low-latency work admitted
- all maintenance work suspended
- new heavy compute blocked
- operator alert emitted immediately

## 12. Timeout, Cancellation, and Retry Law

## 12.1 Timeout classes

### Intake Timeout
Maximum time allowed before batch admission decision.

### Queue Timeout
Maximum time a schedulable unit may wait in queue.

### Execution Timeout
Maximum time allowed for active kernel or stage execution.

### End-to-End Timeout
Maximum total budget for the caller-visible outcome.

## 12.2 Cancellation rules

Cancellation must support:
- pre-admission cancel
- queued cancel
- stage-boundary cancel
- cooperative in-kernel cancel
- stream subscriber disconnect cancel

No kernel or batch strategy may ignore cancellation unless explicitly certified as non-interruptible, and those cases must be rare and documented.

## 12.3 Retry rules

Retries are permitted only when:
- the operation is idempotent, or
- the operation has explicit retry-safe lineage handling

Non-idempotent state transitions require compensation or fail-closed behavior.

Retry metadata must record:
- original attempt ID
- retry count
- backoff class
- reason code
- whether lineage remains authoritative

## 13. Fairness and Anti-Starvation Policy

Because the scheduler is shared infrastructure, fairness is a production requirement.

The runtime must prevent:
- one module monopolizing throughput
- heavy lanes starving low-latency lanes
- shadow or replay traffic displacing authoritative work
- large batches blocking micro-batches indefinitely

## 13.1 Fairness mechanisms

Acceptable mechanisms include:
- weighted fair queuing
- per-module concurrency caps
- lane-based quotas
- aging promotion for long-wait tasks
- burst caps for producer modules

## 13.2 Anti-starvation rule

No eligible workload may be postponed indefinitely without explicit operator policy. Long-wait work must either:
- be promoted,
- be rejected with reason,
- be rescheduled to another lane,
- or be paused with audit visibility.

## 14. Shape Stability and Batch Compatibility

Session 03 established tensor contract and lineage rules. Session 06 adds execution compatibility rules.

Batch formation must respect:
- dtype compatibility
- shape compatibility class
- strategy compatibility
- kernel certification class
- memory ceiling

TensorFlow’s own execution model highlights why this matters: retracing and unstable shapes degrade execution efficiency, while bounded batching and shape discipline improve performance and predictability fileciteturn8file0L1-L1 fileciteturn9file0L1-L1.

## 14.1 Compatibility classes

### Exact Batchable
Same dtype, same shape, same kernel path.

### Shape-Compatible Batchable
Same dtype and operation, dynamic but bounded compatible shapes.

### Requires Padding or Rebatching
May batch only through explicit transformation.

### Non-Batchable
Must remain sequential or isolated.

## 14.2 Rebatching rule

Rebatching is allowed only if:
- tensor lineage remains valid
- caller semantics are preserved
- latency budget remains acceptable
- transformed outputs remain attributable to original units

## 15. Lane Interaction with Module Species

## 15.1 Block Controller Species
- owns scheduling authority, policy visibility, overload signaling
- never performs uncontrolled heavy compute in the control lane

## 15.2 DataEvolution Species
- primary user of transformation and pipeline batches
- must preserve lineage across chained transforms
- must expose repair/drop/reject metrics

## 15.3 TensorTrainer Species
- primary user of heavy compute and staged preprocessing lanes
- must obey hard admission control
- must never preempt controller or low-latency lanes without explicit policy

## 15.4 ML Runtime Species
- serves low-latency and standard inference lanes
- may expose streaming inference under L3 rules
- must certify model-specific batch compatibility ceilings

## 16. Observability and Telemetry Requirements

Every batch subsystem must emit:
- queue depth
- admission latency
- queue wait time
- batch formation time
- execution duration
- batch size distribution
- rejection count
- timeout count
- cancellation count
- retry count
- lane occupancy
- fairness violations
- dropped or dead-lettered units

## 16.1 Mandatory dashboards

1. lane occupancy dashboard  
2. queue depth dashboard  
3. batch size distribution dashboard  
4. timeout and cancellation dashboard  
5. rejection and dead-letter dashboard  
6. per-module scheduler share dashboard  
7. latency p50/p95/p99 by lane  

## 17. Failure Modes and Recovery

### Failure Mode 1 — Queue saturation
Response:
- assert overload class
- throttle or reject lower-priority work
- preserve control lane availability
- emit alert and audit trail

### Failure Mode 2 — Batch explosion
Response:
- split future batches
- lower lane caps
- force compatibility enforcement
- record formation anomaly

### Failure Mode 3 — Cancellation blindness
Response:
- mark kernel non-compliant
- demote from low-latency lanes until fixed
- add certification failure record

### Failure Mode 4 — Starvation
Response:
- age/promotion policy kicks in
- surface scheduler fairness violation
- require capacity review

### Failure Mode 5 — Memory pressure from heavy lanes
Response:
- stop admitting heavy batches
- drain current heavy work if safe
- elevate operator warning
- preserve control and critical inference lanes

## 18. QA and Certification Gates

A scheduler or batch subsystem cannot be promoted without:
- unit tests for queue policy and admission rules
- integration tests for multi-lane scheduling
- cancellation tests
- timeout tests
- overload tests
- fairness tests
- dead-letter tests
- benchmark coverage for hot scheduler paths
- soak tests for queue stability

The repo’s performance posture already requires measurable hot paths and regression sensitivity for critical code, which strongly supports this requirement fileciteturn24file0L1-L1.

## 18.1 Required benchmark classes

- enqueue/dequeue latency
- batch formation latency
- scheduler decision latency
- cancellation propagation latency
- fan-out execution overhead
- memory allocation on hot paths
- throughput under mixed lane pressure

## 19. Session 06 Acceptance Criteria

Session 06 is complete only if:
- batch containers are defined as governed runtime objects
- execution lanes are defined and owned
- queue policies are explicit for all lanes
- backpressure doctrine exists and is testable
- timeout and cancellation semantics are deterministic
- fairness and anti-starvation policy are documented
- TensorTrainer and DataEvolution have lane assignments and protections
- observability metrics and dashboards are defined
- scheduler QA gates are formalized

## 20. Session 06 Final Statement

Session 06 makes throughput a governed property of BCG. From this point onward, performance cannot be described only in terms of model speed or module speed. It must be described in terms of how the runtime shapes work, assigns it to lanes, protects itself under pressure, and preserves traceable execution from intake to result.

The system now advances from a collection of callable modules toward a true tensor execution fabric with:
- governed batch containers
- lane-specific scheduling
- bounded queues
- explicit backpressure
- deterministic cancellation
- fair resource use
- measurable performance

This session is the foundation for later capacity validation, hardening, live updates under load, and production-grade multi-species cooperation under the Block Controller runtime constitution.
