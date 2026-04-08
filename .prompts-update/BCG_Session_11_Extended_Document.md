# Session 11 Extended Document
## Live Runtime, Hot Refresh, and Session Joinability

## 1. Session Purpose

Session 11 defines the runtime continuity constitution of the BCG ecosystem. Its purpose is to ensure that the platform can remain alive while updates, refinements, model swaps, configuration changes, and operational sessions occur. This session establishes the law for safe draining, hot replacement, rolling activation, shell-governed interventions, and user/operator session joinability.

The central principle of this session is simple:

The platform must evolve while running.

This does not mean reckless mutation of production state. It means that runtime continuity becomes a governed capability of the fabric. Changes must be staged, observed, reversible, and bounded by policy. The system must not require total collapse or restart for ordinary iterative improvement.

## 2. Strategic Position

The current repo direction already supports the foundations for this session:
- the Block Controller is the orchestration hub for registration, routing, heartbeat monitoring, and observability aggregation fileciteturn14file0L1-L1
- the platform already assumes a distributed module topology with dedicated HTTP and WebSocket ports and a central orchestration layer fileciteturn13file0L1-L1
- module networking is built around startup registration, ongoing heartbeats, and dynamic connection patterns under a shared network discipline fileciteturn20file0L1-L1
- streaming and bidirectional runtime communications are already part of the project’s intended architecture via WebSocket, SignalR, and inference streams fileciteturn19file0L1-L1

Session 11 turns those foundations into runtime law.

## 3. Session 11 Goals

### Primary Goals
- define what “live runtime” means in the BCG system
- define how modules are updated without total fabric shutdown
- define safe draining, replacement, and rollback behavior
- define how operators and users join runtime sessions safely
- define shell privilege and intervention boundaries
- define the quality gates for any live update capability

### Secondary Goals
- reduce deployment downtime
- preserve control-plane continuity during changes
- make interactive update sessions operationally safe
- ensure live participation does not destabilize the platform

## 4. Session 11 Deliverables

1. runtime continuity constitution  
2. module lifecycle transition law  
3. drain and cutover protocol  
4. hot refresh and hot replacement policy  
5. session joinability model  
6. shell governance rules  
7. live observability and rollback requirements  
8. runtime QA and chaos test checklist  
9. operator permissions matrix  
10. live release acceptance criteria  

## 5. Runtime Continuity Doctrine

## 5.1 Runtime Continuity Defined

Runtime continuity means:
- the Block Controller remains available during ordinary module changes
- modules can be added, drained, replaced, or rolled back without collapsing the ecosystem
- clients and operators can remain attached to live sessions while updates happen
- state transitions are visible, governed, and reversible
- degraded operation is preferred over uncontrolled outage when safe

Runtime continuity does not mean:
- arbitrary in-memory mutation without audit
- unsafe replacement of authoritative state
- bypassing rollout and certification rules
- silent changes to contracts during active execution

## 5.2 Continuity Classes

### Class A — Live-Patchable
Changes safe for near-live application with bounded risk.

Examples:
- non-breaking config updates
- observability tuning
- non-contract admin UI updates
- scheduler weight tuning within allowed policy bands

### Class B — Drain-and-Replace
Changes requiring graceful draining of targeted modules or lanes before replacement.

Examples:
- runtime service binary updates
- model activation swaps
- batch executor logic updates
- transformation pipeline version changes

### Class C — Controlled Restart
Changes that require explicit restart of affected species but not total fabric failure.

Examples:
- kernel ABI changes
- transport stack replacement
- certificate reload requiring process restart
- storage driver upgrade within one species

### Class D — Coordinated Maintenance
Changes requiring platform-level maintenance window or major orchestration control.

Examples:
- primary database migration
- breaking transport envelope changes
- cross-species contract evolution requiring synchronized compatibility handling

## 6. Block Controller Authority in Live Runtime

The Block Controller becomes the runtime continuity governor. It already governs registration, lifecycle, routing, and heartbeat logic in the project direction fileciteturn14file0L1-L1. Session 11 extends that into live update authority.

The Block Controller governs:
- species admission and routeability
- drain start and drain completion recognition
- traffic cutover between generations
- rollback eligibility and activation
- session broadcast of runtime state changes
- degraded mode declarations
- protection of critical lanes during replacement events

No species may self-declare production routeability after update without controller acknowledgment.

## 7. Lifecycle Transition Law

Every module species must support these runtime phases:

1. Bootstrapping  
2. Registering  
3. Warming  
4. Active  
5. Protected Active  
6. Draining  
7. Suspended  
8. Replacing  
9. Rolled Back  
10. Archived  
11. Dead  

### Phase Definitions

#### Bootstrapping
Process is starting but not yet visible as routeable.

#### Registering
Species announces identity, capabilities, and runtime metadata to the Block Controller.

#### Warming
Species loads kernels, models, caches, subscriptions, or schema state but does not yet receive general production traffic.

#### Active
Species is routeable under ordinary policy.

#### Protected Active
Species is active but shielded from certain low-priority or experimental traffic because of critical work or elevated risk.

#### Draining
Species accepts no new work of targeted classes and completes or hands off bounded in-flight work.

#### Suspended
Species remains registered but intentionally non-routeable.

#### Replacing
New generation exists; controller coordinates cutover.

#### Rolled Back
Prior generation restored as routeable due to failure or policy breach.

#### Archived
Historical generation preserved for reference and replay but not routeable.

#### Dead
Species is terminated and removed from active control lanes.

## 8. Drain and Cutover Protocol

## 8.1 Drain Intent

A drain operation must declare:
- target species and instance
- traffic classes to block
- whether in-flight work is completed, checkpointed, or handed off
- maximum drain time
- rollback policy if drain exceeds budget

## 8.2 Drain Rules
- no new incompatible work may enter draining lanes
- critical in-flight state must not be silently discarded
- drain progress must be observable
- controller must publish drain state changes to observability and operator channels
- drain timeout must trigger explicit escalation, not silent hang

## 8.3 Cutover Rules
A replacement cutover requires:
- new generation registered and warmed
- compatibility validated
- health and readiness confirmed
- route weights adjusted by controller
- rollback target preserved until stabilization window passes

## 8.4 Stabilization Window
After cutover, a bounded stabilization window applies. During this window:
- rollback remains privileged and fast
- telemetry scrutiny is elevated
- experimental traffic may remain limited
- model or kernel confidence thresholds may be tighter

## 9. Hot Refresh and Hot Replacement

## 9.1 Hot Refresh

Hot refresh applies to state, configuration, and bounded runtime changes that do not require species replacement.

Allowed examples:
- threshold tuning
- route weights
- logging and metrics thresholds
- subscription filters
- dashboard refresh rules

Rules:
- refresh must be versioned
- refresh must be auditable
- refresh must be reversible
- refresh must not violate contract stability

## 9.2 Hot Replacement

Hot replacement applies when one generation of a species is superseded by another while the fabric remains alive.

Required steps:
1. new generation boots
2. new generation registers
3. readiness checks pass
4. old generation enters draining
5. route weights shift
6. stabilization window begins
7. old generation archives or terminates after success

## 9.3 Prohibited Live Actions
The following are forbidden unless explicitly entering coordinated maintenance:
- silent envelope/schema breaks
- unversioned model signature changes
- replacing authoritative control-plane semantics without migration
- operator-side memory poking without audit
- shell commands that mutate live runtime beyond declared privilege bands

## 10. Session Joinability Doctrine

## 10.1 Session Types

### Operator Sessions
Used by privileged users for observability, maintenance, rollout, rollback, and troubleshooting.

### Engineering Update Sessions
Used during structured update progression where controlled changes are being applied to live non-critical or staged lanes.

### User Runtime Sessions
Used by end users or platform users to observe or interact with live runtime behavior through approved surfaces.

## 10.2 Joinability Rules
- all joinable sessions must be represented as explicit runtime session objects
- all participants must have role-bound permissions
- all live views must be filtered and rate-governed
- no session join may bypass transport, identity, or audit rules
- session visibility must respect species boundaries and data sensitivity

## 10.3 Session Channels
Joinable sessions may expose:
- live module health
- block and kernel state summaries
- active route map summaries
- selected tensor lineage views
- event streams
- rollout progress
- rollback status
- controlled shell command outputs

## 11. Shell Governance

The shell remains a powerful runtime species. Session 11 defines its operational law.

### Shell Roles
- observer
- operator
- maintainer
- emergency authority

### Shell Permissions
Observer:
- read-only state inspection
- no mutations

Operator:
- start approved session workflows
- request drain, observe cutover, trigger bounded safe commands

Maintainer:
- approve hot refresh classes
- activate replacements
- manage rollback within policy

Emergency Authority:
- invoke emergency isolation
- force protective suspension
- terminate compromised lanes when policy requires

### Shell Rules
- every command must have trace ID and operator identity
- every mutating command must be auditable
- destructive commands require elevated privilege
- shell outputs must be capturable into operational history
- shell cannot become a backdoor around controller policy

## 12. Session Joinability and UI/Operator Plane

The Blazor + Fluent UI operator surface remains the approved human-facing participation plane. Session 11 requires that live session joinability be represented through safe operator views rather than ad hoc internal tunnels.

The UI must support:
- live routeability views
- drain/cutover dashboards
- session presence indicators
- runtime event feeds
- rollback readiness indicators
- explicit state transitions for species generation changes

The UI must not:
- expose raw uncontrolled event storms
- allow unsafe commands without role validation
- hide degraded state during cutover events

## 13. State Handling During Live Updates

## 13.1 Ephemeral State
Ephemeral state may be discarded during replacement if policy allows, but loss must be classified and visible.

## 13.2 Recoverable State
Recoverable state must either:
- finish,
- checkpoint,
- hand off,
- or restart under declared semantics

## 13.3 Authoritative State
Authoritative state changes must not be tied to uncontrolled process lifetime. Persistent state must remain valid across generations.

## 13.4 Tensor and Batch State
- hot tensor cache may be repopulated after replacement
- in-flight batches require explicit handoff or bounded restart policy
- lineage must survive if the operation is resumable or replayable
- silent duplication of side effects is forbidden

## 14. Live Observability Requirements

A live runtime species must emit enough signals to answer:
- which generation is active
- what phase each species instance is in
- whether draining is progressing
- whether cutover is stable
- whether rollback remains available
- whether user/operator sessions are attached
- whether a session is producing abnormal load
- whether live changes correlate with degradation

### Required Signals
- generation ID
- runtime phase
- drain started / drain completed
- readiness passed / readiness failed
- cutover initiated / cutover completed
- rollback initiated / rollback completed
- session joined / session left
- shell command executed
- stabilization window opened / closed

## 15. Failure Model

## 15.1 Failed Warm-Up
If replacement generation fails to warm:
- old generation remains routeable
- replacement is marked failed
- cutover is blocked
- operator-visible alert is emitted

## 15.2 Failed Drain
If old generation cannot drain within policy:
- escalation occurs
- controller may extend drain, checkpoint state, or suspend cutover
- forced termination requires elevated policy and side-effect review

## 15.3 Failed Cutover
If post-cutover telemetry breaches thresholds:
- rollback may activate during stabilization window
- new generation route weights collapse
- old generation can be restored if preserved

## 15.4 Session Overload
If operator or user session activity overloads live runtime:
- session stream rate limiting applies
- degraded detail mode may activate
- non-essential session feeds may be suspended

## 16. QA and Certification Gates

No species may claim Session 11 compliance without:
1. lifecycle phase implementation  
2. readiness and drain semantics  
3. cutover and rollback plan  
4. live telemetry coverage  
5. session identity and role checks  
6. shell auditability  
7. chaos test for failed warm-up or failed cutover  
8. proof that controller remains authoritative during replacement  
9. operator documentation and runbook updates  

## 17. Acceptance Criteria

Session 11 is complete only if:
- runtime continuity is formally defined
- drain and cutover are governed
- hot refresh and hot replacement classes are defined
- session joinability has explicit rules
- shell authority is role-bound and auditable
- observability signals for live change are declared
- rollback is explicit and policy-bound
- no uncontrolled live mutation path remains

## 18. Session 11 Final Statement

Session 11 makes runtime continuity part of the BCG constitution. The platform is no longer allowed to depend on collapse-and-restart as its ordinary method of evolution. Instead, modules must warm, register, drain, replace, stabilize, and roll back under controller authority. Users and operators may join live sessions, but only through governed, observable, role-bound channels. The system remains alive not because it is uncontrolled, but because it is disciplined.
