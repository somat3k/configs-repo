# Session 15 Extended Document
## Observability, Telemetry, and Runtime Forensics

## 1. Session Purpose

Session 15 defines the observability constitution of the BCG ecosystem. Its purpose is to make the platform understandable while it is alive, diagnosable when it degrades, and reconstructable after failure or incident. This session turns logs, metrics, traces, events, lineage records, and operational evidence into a governed runtime capability rather than a scattered collection of dashboards and ad hoc debug outputs.

The central principle of this session is:

If the platform cannot explain what happened, why it happened, where it happened, and what changed, then it is not production-grade.

Observability in BCG is broader than service metrics. It must cover:
- controller decisions
- module lifecycle transitions
- kernel execution
- batch progression
- tensor lineage
- transport selection and stream state
- storage routing
- artifact activation
- operator sessions
- shell interventions
- rollback and recovery events

Session 15 therefore defines how runtime truth becomes measurable, queryable, and forensically usable.

## 2. Strategic Position

The existing repo direction already supports the foundations for this session:
- the Block Controller is the orchestration hub with lifecycle, routing, heartbeat, and observability aggregation responsibilities fileciteturn14file0L1-L1
- the architecture already positions the platform as a controller-centered distributed module network rather than a monolith fileciteturn13file0L1-L1 fileciteturn17file0L1-L1
- the networking and streaming skills already assume live channel participation, bounded producer-consumer paths, and runtime message discipline fileciteturn20file0L1-L1 fileciteturn19file0L1-L1
- the storage split across Redis, PostgreSQL, and IPFS already implies multiple evidence layers for hot state, authority, and immutable artifacts fileciteturn22file0L1-L1

Session 15 turns those foundations into a formal evidence and telemetry fabric.

## 3. Session 15 Goals

### Primary Goals
- define the observability model for all major BCG species
- define telemetry classes and what each one is used for
- define traceability across controller, transport, tensor, storage, and execution paths
- define forensic-grade event preservation and incident reconstruction rules
- define alerting, SLO, and operational dashboard law
- define what evidence is required before, during, and after runtime incidents

### Secondary Goals
- reduce blind spots across the control fabric
- eliminate guesswork during failures and degradations
- make rollback, cutover, and live sessions measurable
- ensure tensor-native flows remain explainable, not opaque

## 4. Session 15 Deliverables

1. observability constitution  
2. telemetry taxonomy  
3. trace and correlation model  
4. tensor lineage observability law  
5. controller and species dashboard requirements  
6. alerting and SLO doctrine  
7. runtime forensic evidence model  
8. incident and post-incident reconstruction rules  
9. observability QA and certification gates  
10. acceptance criteria for observable species  

## 5. Observability Doctrine

Observability in BCG is the governed ability to answer, at runtime and after runtime:
- what is happening
- where it is happening
- why it is happening
- what changed before it happened
- which species, workload, or artifact is responsible
- how the system reacted
- whether the system can safely continue, degrade, or roll back

Observability must exist across:
- control plane
- execution plane
- transport plane
- storage plane
- security and trust plane
- operator and session plane
- artifact and model plane

No major capability may exist without a declared observability footprint.

## 6. Telemetry Taxonomy

## 6.1 Metrics

Metrics answer:
- how much
- how often
- how fast
- how full
- how many failures

Metrics are used for:
- SLO tracking
- saturation detection
- capacity planning
- regressions
- alert thresholds

Examples:
- route decision latency
- heartbeat freshness
- queue depth
- batch throughput
- tensor cache hit rate
- model inference latency
- stream backpressure count
- artifact activation duration

## 6.2 Logs

Logs answer:
- what was reported
- what path was taken
- which branch or policy decision executed
- what reason strings or error contexts existed

Logs are used for:
- detailed local reasoning
- event context
- operator and species narratives
- audit-like but lower-structure explanations

Logs must not be treated as the sole durable source of truth for critical control decisions if structured events are required.

## 6.3 Traces

Traces answer:
- what path a workload followed across species
- where time was spent
- where latency or failure entered
- which downstream dependencies participated

Traces are used for:
- cross-species causality
- performance analysis
- service dependency understanding
- execution path reconstruction

Traces must carry correlation identifiers that survive controller routing, transport transitions, storage routing, and tensor lineage boundaries where relevant.

## 6.4 Structured Events

Structured events answer:
- what state transition occurred
- what kind of operational fact became true
- who or what initiated it
- what objects were affected

Examples:
- species registered
- species entered draining
- batch accepted
- tensor externalized
- artifact activated
- session joined
- rollback initiated
- routeability revoked

Structured events are the main fabric for runtime forensics and operational reconstruction.

## 6.5 Lineage Records

Lineage records answer:
- where a tensor, artifact, transformation, or model state came from
- which transformation or execution stages changed it
- which upstream and downstream identities relate to it

Lineage records are required because BCG is not just a service platform. It is a tensor-governed, transformation-heavy, model-bearing control fabric.

## 7. Correlation and Trace Identity Model

## 7.1 Correlation Classes

### Request Correlation
Used for ordinary control-plane and request-response flows.

### Stream Correlation
Used for long-lived streaming channels and resumable session flows.

### Batch Correlation
Used for grouped execution containers and multi-task runtime bundles.

### Tensor Correlation
Used for tensor lineage, mutation-free derivations, transformations, and storage routing.

### Artifact Correlation
Used for model, bundle, and signed object activation, rollback, and verification.

### Operator Session Correlation
Used for human-attached sessions and shell or UI actions.

## 7.2 Correlation Rules
- controller decisions must carry correlation IDs
- species-local logs must include shared correlation when participating in routed work
- tensor lineage references must bridge to workload traces where meaningful
- artifact activation traces must connect rollout, verification, and serving lanes
- security-sensitive actions must preserve operator and session correlation

## 8. Block Controller Observability Law

The Block Controller is the global runtime governor and must therefore provide the highest observability density.

The controller must expose enough data to answer:
- which species are registered
- which species are healthy, warming, active, protected, draining, suspended, or failed
- why a route was chosen
- why a route was denied
- what degraded mode is active
- which policies were in effect
- whether replacement or rollback is underway
- whether stale discovery or trust issues affected routing

### Required Controller Signals
- registration requested
- registration accepted
- registration denied
- heartbeat fresh
- heartbeat stale
- route selected
- route rejected
- route degraded
- drain started
- drain completed
- cutover initiated
- cutover stabilized
- rollback initiated
- rollback completed
- policy changed
- incident state raised

## 9. Species-Level Observability Law

Every species must define:
- health signals
- lifecycle signals
- workload signals
- error classes
- performance metrics
- storage interaction summaries
- transport interaction summaries
- trust and authorization signals where relevant

Species observability must be sufficient to explain:
- what the species accepted
- what it rejected
- what it processed
- what it emitted
- what it depended on
- why it degraded or failed

## 10. Tensor Lineage Observability

This is a core requirement unique to the BCG direction.

## 10.1 Why Tensor Lineage Must Be Observable

Tensors in BCG are not disposable anonymous blobs. They are:
- execution inputs
- transformation outputs
- training carriers
- inference outputs
- graph communication structures
- lineage-bearing objects

If tensors are not observable, the entire tensorification strategy becomes opaque and untrustworthy.

## 10.2 Required Tensor Evidence
For eligible tensor classes, the system must be able to answer:
- tensor identity
- origin species or block
- source trace
- dtype and shape
- transformation chain
- storage routing decisions
- externalization status
- consumption history at high level
- artifact or model relation where applicable

## 10.3 Tensor Visibility Rules
- hot observability may summarize tensors instead of exposing payloads
- large tensor payloads should not flood operator sessions
- sensitive tensors may require redacted or metadata-only views
- lineage must remain reconstructable even if payload retention is bounded

## 11. Batch and Scheduler Observability

Batch execution must expose:
- batch identity
- lane class
- admission time
- queue time
- execution start
- partial progress
- completion or failure
- cancellation reason
- retry or replay status
- storage side effects if materialized

Scheduler observability must expose:
- queue depth by class
- starvation risk
- fairness actions
- backpressure state
- lane saturation
- deadline misses
- priority inversion incidents

## 12. Transport and Streaming Observability

The streaming fabric must expose:
- transport selected
- stream identity
- publish and subscribe state
- session participants
- reconnects
- replay usage
- resume usage
- dedupe actions
- dropped or rejected messages
- backpressure events
- degraded detail mode

This directly aligns with the repo’s WebSocket and streaming direction where live runtime messaging and channel governance are core system behavior fileciteturn19file0L1-L1 fileciteturn20file0L1-L1

## 13. Storage Observability

Storage telemetry must distinguish:
- cache paths
- authoritative persistence paths
- immutable artifact/object paths

The system must be able to answer:
- was the object cached, persisted, or externalized
- why that storage class was selected
- whether the write succeeded, retried, degraded, or failed
- whether replay or restoration depends on the object
- whether the object is still fresh, retained, pinned, or expired

This aligns with the repo’s storage split across Redis, PostgreSQL, and IPFS fileciteturn22file0L1-L1

## 14. Security and Trust Observability

Trust-sensitive events must be observable, including:
- species admission decisions
- session start and revocation
- artifact verification and activation
- policy denials
- authorization failures
- shell elevation and mutating commands
- secret rotation
- emergency isolation

Observability must never erase security context from critical actions.

## 15. Dashboard Doctrine

Dashboards are not ornamental. They are controlled runtime instruments.

## 15.1 Required Dashboard Families

### Controller Dashboard
Shows:
- species states
- route decisions
- degraded modes
- cutovers
- rollbacks
- policy state
- stale or contested identities

### Execution Dashboard
Shows:
- kernel and block activity
- batch admission and completion
- queue pressure
- timeout patterns
- lane saturation

### Tensor Dashboard
Shows:
- lineage summaries
- transformation volumes
- shape and dtype summaries
- storage routing counts
- externalization rates

### Streaming Dashboard
Shows:
- active streams
- reconnects
- backpressure
- dropped or rejected events
- session load

### Storage Dashboard
Shows:
- Redis hit rate and pressure
- PostgreSQL write and query latency
- IPFS fetch and pin state
- replay readiness indicators

### Trust Dashboard
Shows:
- admission failures
- revoked sessions
- artifact trust state
- authorization denials
- isolation actions

## 15.2 Dashboard Rules
- dashboards must not destabilize runtime
- dashboards must prefer summaries over raw firehoses
- operator roles control visibility depth
- degraded detail mode may activate under pressure
- critical signals must remain visible during incidents

## 16. Alerting and SLO Doctrine

## 16.1 Alert Classes

### Informational
Non-urgent, requires awareness only.

### Warning
Potential degradation or drift requiring attention.

### Critical
Immediate operational risk or active failure.

### Emergency
Severe threat to control, trust, or data integrity.

## 16.2 SLO Domains
The system should declare SLOs for:
- route decision latency
- species availability
- batch completion latency
- inference latency
- stream continuity
- cache hit rate
- replay readiness
- artifact activation success
- session joinability reliability

## 16.3 Alert Rules
- alerts must map to runbooks
- repeated noise without operator action path is not acceptable
- alerts must include correlation and scope
- critical alerts should identify likely impacted species, workloads, and lanes
- alert suppression policies must be visible and bounded

## 17. Runtime Forensics Doctrine

## 17.1 Purpose of Runtime Forensics

Runtime forensics is the ability to reconstruct significant platform events after the fact, including:
- incidents
- misroutes
- degraded mode entry
- failed cutovers
- unauthorized actions
- unexplained performance regressions
- tensor lineage anomalies
- artifact trust issues

## 17.2 Forensic Evidence Requirements
For major incidents, the system should be able to reconstruct:
- time-ordered event sequence
- actor identities
- affected species
- affected workloads or tensors
- controller decisions
- transport path
- storage side effects
- artifact or model relation
- rollback or recovery actions
- unresolved evidence gaps

## 17.3 Evidence Classes
- structured events
- traces
- audit records
- selected logs
- lineage records
- snapshot references
- replay references
- metric anomalies

## 17.4 Forensic Integrity Rules
- evidence must be time-correlated
- evidence classes must be linkable by identity
- evidence retention must match incident value
- missing evidence must be visible, not silently ignored
- operator or shell actions must not evade evidence capture

## 18. Post-Incident Reconstruction

After a significant incident, the system should support production of:
- incident timeline
- affected scope map
- root-cause candidate graph
- controller policy state at time of incident
- contributing transport and storage behavior
- trust and artifact state if relevant
- mitigation steps taken
- residual risk after mitigation

This is not optional for a production-grade control fabric.

## 19. Failure Model

## 19.1 Silent Failure
If a species fails without sufficient telemetry:
- that is considered an observability failure, not merely a species failure

## 19.2 False Green State
If dashboards show healthy while underlying routeability or work completion is failing:
- observability integrity breach is declared

## 19.3 Missing Correlation
If logs, events, and traces cannot be joined during critical incidents:
- forensic reconstruction is degraded
- the missing join path becomes a remediation priority

## 19.4 Telemetry Overload
If telemetry threatens runtime stability:
- degrade detail
- preserve critical signals
- protect control and execution lanes before preserving convenience streams

## 20. QA and Certification Gates

No species may claim Session 15 compliance without:
1. declared metrics, logs, traces, and events model  
2. correlation and trace identity notes  
3. lifecycle observability coverage  
4. storage and transport telemetry coverage  
5. dashboard requirements for its critical flows  
6. alert and SLO mapping  
7. failure injection proving observable degradation  
8. forensic reconstruction notes for major incidents  
9. operator-visible runbook linkage for critical alerts  

## 21. Acceptance Criteria

Session 15 is complete only if:
- telemetry taxonomy is defined
- correlation rules exist across controller, species, and workload boundaries
- tensor lineage observability is explicit
- dashboard families are defined
- alerting and SLO doctrine are declared
- runtime forensic evidence model exists
- post-incident reconstruction requirements are defined
- observability QA and certification gates are explicit

## 22. Session 15 Final Statement

Session 15 gives the BCG ecosystem operational sight and memory. The system becomes able not only to run, but to explain itself. The controller can justify routes. Species can justify actions. Tensors can justify lineage. Storage can justify placement. Sessions can justify who was watching and who was acting. When incidents happen, the platform does not merely recover. It remembers, reconstructs, and learns under evidence.
