# Session 02 Extended Document
## Block Controller Evolution into the Runtime Governor

## 1. Session Purpose

Session 02 upgrades the Block Controller from a central orchestration hub into the governing execution authority for the BCG fabric. This session does not replace the current Block Controller role. It expands it from registration, routing, and health awareness into policy-driven runtime governance across block execution, tensor routing, scheduling, module lifecycle, and live update continuity.

This document defines the production-grade operating model for that evolution.

## 2. Session 02 Strategic Position

The existing project direction already identifies the Block Controller as the root orchestration module. It is responsible for module registration and lifecycle management, message routing, heartbeat monitoring, global state coordination, and observability aggregation. Session 02 preserves those duties and adds execution authority.

The controller must now become the runtime governor for:
- module capability resolution
- execution policy selection
- load-aware routing
- degradation and failover behavior
- tensor route admissibility
- batch lane selection
- live update safety
- shell-governed runtime actions
- cross-module execution continuity

This means the Block Controller is no longer a passive traffic node. It becomes the authoritative control-plane service for the whole BCG execution ecosystem.

## 3. Session 02 Goals

### Primary Goals
- evolve the Block Controller into a runtime governor
- formalize controller-side authority over execution decisions
- define capability-aware and health-aware routing
- define controller interaction with tensors, blocks, kernels, and batch lanes
- define safe live-runtime management rules
- define controller QA, observability, and rollback responsibilities

### Secondary Goals
- reduce future architecture drift
- stabilize module admission and module replacement policies
- prepare the controller to govern TensorTrainer and DataEvolution species
- create the operating base for later high-throughput and high-complexity module interactions

## 4. Session 02 Scope

### In Scope
- controller governance model
- controller authority boundaries
- runtime routing policy
- execution admission rules
- block and kernel governance boundaries
- module capability registry
- heartbeat and health escalation rules
- degradation and failover policy
- shell-controlled runtime commands policy
- dynamic route selection foundations
- controller telemetry and SLO definition

### Out of Scope
- deep implementation of all module-specific execution paths
- complete TensorTrainer implementation
- complete DataEvolution implementation
- full UI realization beyond controller-facing operator requirements
- post-session capacity hardening

## 5. Block Controller Mission After Session 02

The Block Controller exists to maintain order, continuity, and governed execution across a network of independent module species.

Its mission is to:
- admit modules into the fabric
- maintain a capability-aware live registry
- decide where execution should occur
- reject unsafe or undefined execution requests
- route tensors and block requests under policy
- preserve runtime continuity during updates and faults
- expose operational truth to the UI, shell, and observability systems
- coordinate controlled degradation rather than uncontrolled failure

## 6. Governing Principles

### 6.1 Control Plane over Data Plane
The controller governs the fabric. It does not need to process every heavy payload itself, but it must know enough to decide where and how work is allowed to execute.

### 6.2 No blind routing
No execution request should be routed merely because a target exists. A route must satisfy:
- capability compatibility
- health admissibility
- policy admissibility
- transport admissibility
- tensor admissibility where relevant
- load and priority constraints

### 6.3 Runtime continuity beats convenience
The controller must prefer decisions that preserve live operation. Fast but destabilizing behavior is inferior to slower but continuity-safe behavior.

### 6.4 Species remain independent
The controller does not dissolve module independence. It governs inter-species order while preserving per-module autonomy.

### 6.5 Every decision must be explainable
Every route, rejection, failover, drain, and promotion decision must be attributable, observable, and logged.

## 7. Functional Evolution of the Controller

## 7.1 Current Baseline Role
The current controller baseline already includes:
- module registration
- lifecycle awareness
- message routing
- heartbeat monitoring
- global coordination
- observability aggregation

## 7.2 New Runtime Governor Role
Session 02 adds:
- execution intent evaluation
- block capability registry
- kernel route classification
- batch lane selection
- health-based route scoring
- admission control
- controlled degraded mode
- maintenance and drain mode
- live module replacement policy
- shell-governed runtime action approval
- policy-based transport selection

## 8. Controller Authority Model

## 8.1 The Controller Owns
- module admission
- module deregistration
- routeability state
- capability index
- health truth for routing purposes
- execution policy tables
- runtime mode state
- degradation declarations
- maintenance windows
- route rejection reasons
- system event broadcasts

## 8.2 The Controller Does Not Own
- per-module internal business logic
- per-module model internals
- per-module kernel implementation details
- per-module internal memory strategy unless it affects route safety
- per-module private artifacts beyond governance metadata

## 8.3 Shared Ownership Zones
Shared ownership exists for:
- tensor contracts
- transport compatibility
- observability metadata
- execution status propagation
- rollback events
- admission certification

## 9. Controller Subsystems Introduced in Session 02

### 9.1 Module Registry
A live registry storing:
- module ID
- species name
- endpoint map
- supported operations
- supported tensor classes
- health state
- load state
- policy labels
- version and compatibility metadata
- runtime mode

### 9.2 Capability Resolver
Determines which modules are eligible to execute a request.

Eligibility dimensions:
- operation support
- tensor support
- transport support
- environment compatibility
- version compatibility
- security posture
- current routeability

### 9.3 Health Governor
Tracks:
- heartbeat freshness
- startup readiness
- degraded mode declarations
- dependency impairment
- controller-observed failures
- operator-imposed maintenance state

### 9.4 Route Governor
Scores and selects target modules according to:
- capability match
- health score
- load score
- locality or network rule
- runtime policy
- operator constraints
- canary or rollout rules

### 9.5 Execution Policy Engine
Evaluates:
- sync vs async eligibility
- sequential vs parallel vs pipeline preference
- routing priority
- fallback permissibility
- admission or rejection
- retry policy
- timeout class

### 9.6 Runtime Mode Manager
Maintains controller-declared modes:
- normal
- degraded
- maintenance
- draining
- recovery
- incident

## 10. Routeability Model

A module may exist in the registry but still be non-routeable.

### Routeable
The module is healthy, policy-admissible, and available for execution.

### Restricted
The module may receive only specific workloads or traffic classes.

### Draining
The module may finish current work but must not receive new work except explicitly authorized control-plane actions.

### Maintenance
The module is deliberately withheld from normal routing.

### Degraded
The module may still receive lower-risk workloads or fallback workloads if policy allows.

### Quarantined
The module remains visible for investigation but cannot receive any execution workload.

## 11. Capability Resolution Standard

Every module must advertise capabilities in controller-readable form.

### Required capability dimensions
- module species
- operation types
- tensor classes accepted
- tensor classes produced
- transport interfaces supported
- batch support level
- streaming support level
- required dependencies
- acceleration profile if relevant
- statefulness declaration

### Capability declaration quality requirements
- machine-readable
- versioned
- validated at registration time
- rejected if incomplete or ambiguous

## 12. Health and Heartbeat Governance

## 12.1 Heartbeat Model
Each module must send heartbeats at governed intervals. The controller tracks:
- last heartbeat received
- heartbeat jitter
- readiness state
- health payload validity
- recent route failures
- recent transport failures

## 12.2 Health States
- healthy
- initializing
- degraded
- unstable
- maintenance
- draining
- failed
- quarantined

## 12.3 Escalation Rules
Examples:
- missed heartbeat threshold triggers unstable state
- repeated execution failures trigger degraded or quarantined state
- operator maintenance command triggers maintenance state immediately
- graceful shutdown with drain acknowledgment triggers draining state

## 13. Execution Admission Model

Before routing a request, the controller must answer:
- Is the request valid?
- Is the payload admissible?
- Is there a capable module?
- Is there a healthy module?
- Is the workload allowed in the current runtime mode?
- Is there enough route confidence to proceed?
- Is fallback or transformation required first?

If any answer is negative, the controller must reject or defer, never guess.

## 14. Controller Interaction with Tensor Workloads

Although heavy tensor handling should not always terminate at the controller, the controller must understand enough tensor metadata to govern safely.

### Controller-visible tensor metadata
- tensor ID
- dtype
- shape
- trace ID
- source species or origin block
- declared tensor class
- persistence handle or externalization marker
- declared sensitivity/security label
- transformation history marker if relevant

### Controller tensor decisions
- route allowed
- route denied
- transform required before route
- externalization required due to size
- batch merge allowed
- streaming only
- asynchronous lane required

## 15. Controller and Batch Lanes

The controller must classify execution into lanes.

### Lane A — Immediate sync control work
Low-latency requests with strict response expectations.

### Lane B — Standard synchronous execution
Normal request-response execution for module workloads.

### Lane C — Batch execution
High-throughput workloads where the scheduler chooses sequential, parallel, or pipeline patterns.

### Lane D — Streaming execution
Long-lived or partial-result workloads.

### Lane E — Deferred/asynchronous job execution
Large or expensive workloads such as training, transformation, or offline graph evolution.

The controller may recommend or require a lane based on:
- workload type
- tensor profile
- size
- urgency
- current runtime mode
- destination species policy

## 16. Controller and Live Runtime Governance

## 16.1 Draining Rules
When a module is updated or prepared for replacement:
- it is marked draining
- no new normal workloads are assigned
- current workloads are allowed to finish subject to timeout policy
- all drain transitions are observable

## 16.2 Replacement Rules
A replacement module must:
- register
- declare compatible capability set
- pass readiness checks
- satisfy routeability admission
before receiving live traffic.

## 16.3 Failover Rules
If a target module becomes unavailable:
- use an alternate capable route if policy permits
- downgrade traffic class if policy allows
- hold or reject requests when no safe path exists
- broadcast state changes to operator surfaces

## 16.4 Session Joinability
The controller must expose enough state for operators and users to:
- observe active modules
- inspect health and routeability
- watch execution lane activity
- monitor drains, failovers, and maintenance windows
- join live update sessions in read or approved control modes

## 17. Shell Governance under the Controller

The shell is a privileged runtime interface but must not bypass controller governance.

### Shell-controlled actions that require controller mediation
- spawn or clone runtime species
- mark maintenance
- initiate drain
- promote routeability
- quarantine species
- override route policy
- execute emergency stop on a route class

### Shell operating conditions
- audit every privileged action
- support dry-run on policy-changing commands
- require explicit authorization for destructive commands
- expose rollback notes for risky actions

## 18. Transport Governance Responsibilities of the Controller

The controller must know transport classes and their allowed usage.

### gRPC / Protobuf
Preferred for typed, strongly governed, service-to-service control and execution paths.

### WebSockets / SignalR
Preferred for live subscriptions, eventing, operator views, and stream-oriented interactions.

### Webhooks
Used for ingress triggers from external sources or compatibility systems.

### HTTP APIs
Used for management, health, compatibility endpoints, and selected sync request classes.

The controller must prevent transport misuse, for example:
- no large high-frequency payload flood over unsuitable lanes
- no streaming downgrade to sync request-response without explicit policy
- no schema-ambiguous payload path in production mode

## 19. Observability and SLO Requirements for the Controller

## 19.1 Mandatory Telemetry
- module registry changes
- heartbeat transitions
- route decisions
- route rejections
- batch lane selections
- failover events
- drain events
- maintenance declarations
- policy overrides
- shell interventions
- transport errors
- execution latency by lane

## 19.2 SLO Drafts
- route decision latency p95 under 10 ms for standard control-plane decisions
- heartbeat state convergence under 15 seconds for failure visibility
- registry consistency visible in operator plane within 5 seconds of module state change
- failover route or explicit rejection within policy timeout budget

## 19.3 Forensic Requirements
Every route decision must be reconstructable by:
- trace ID
- request ID
- policy version
- selected module
- rejected module candidates
- health state at decision time
- runtime mode at decision time

## 20. Security and Trust Conditions

Session 02 requires controller-governed trust gates even if full security architecture is completed later.

### Minimum rules now
- anonymous module admission is not acceptable in production
- module identity must be unique and attributable
- capability declarations must be signed or otherwise trusted in later sessions
- privileged shell actions must be logged
- route overrides must be attributable to an operator identity

## 21. Quality Assurance Plan for Session 02

### Test classes required
- registry tests
- capability resolution tests
- heartbeat timeout tests
- routing policy tests
- degraded mode tests
- draining tests
- failover tests
- shell authorization tests
- observability emission tests
- backward compatibility tests for registration and route contracts

### Failure drills required
- missing heartbeat
- duplicate module registration
- route to incompatible module attempt
- module degradation during active routing
- controller restart or recovery scenario
- replacement module registration during drain window

### Exit QA gates
- controller decisions are deterministic for the same policy inputs
- route rejection reasons are structured and visible
- health transitions are observable
- maintenance and draining behavior are testable and documented
- shell actions cannot silently violate runtime policy

## 22. Required Documents Triggered by Session 02

For the Block Controller species, Session 02 must update or create at minimum:
1. charter
2. architecture overview
3. runtime contract
4. capability registry spec
5. routing policy spec
6. health model
7. transport matrix
8. execution lane policy
9. observability and telemetry
10. performance budget
11. QA strategy
12. rollout, drain, and failover runbook
13. security and trust notes
14. change history
15. roadmap linkage to later sessions

## 23. Risks Identified in Session 02

### Risk 1 — Controller bloat
If the controller absorbs data-plane work rather than governing it, it becomes a bottleneck.

### Risk 2 — Passive routing persistence
If the controller remains a simple router, later tensor and batch governance will fragment into module-local hacks.

### Risk 3 — Non-deterministic failover
Without route policy and health discipline, failover will be inconsistent and hard to debug.

### Risk 4 — Uncontrolled live updates
Without drain and replacement rules, hot updates will degrade reliability rather than improve it.

### Risk 5 — Capability ambiguity
If module capability declarations remain vague, later advanced modules like TensorTrainer and DataEvolution will be difficult to route safely.

## 24. Session 02 Exit Criteria

Session 02 is complete only when:
- the controller authority model is documented
- routeability states are defined
- capability resolution rules are documented
- health and heartbeat escalation rules are defined
- execution lane policy is defined
- live drain and replacement logic is documented
- shell governance boundaries are documented
- controller observability expectations are defined
- QA gates are defined
- follow-on sessions can safely build against this controller constitution

## 25. Final Statement

Session 02 is the formal elevation of the Block Controller into the runtime governor of the BCG ecosystem. After this session, the system should no longer think of the controller as a registration endpoint plus message relay. It becomes the policy-bearing authority that keeps module species interoperable, routeable, observable, and safe under real production conditions.

This session establishes the control-plane constitution required for the later tensor-native, transformation-heavy, high-throughput, and live-updating system that BCG is intended to become.
