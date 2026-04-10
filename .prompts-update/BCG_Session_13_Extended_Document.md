# Session 13 Extended Document
## Streaming Fabric: WebSockets, gRPC Streams, Webhooks, and API Inference

## 1. Session Purpose

Session 13 defines the streaming constitution of the BCG ecosystem. Its purpose is to unify all live and near-live communication paths under one transport-performance law so that real-time data movement becomes governed, observable, resumable, and production-safe.

The BCG platform is not a single-protocol system. It must support:
- WebSocket and SignalR sessions for operator views, live eventing, and bidirectional control
- gRPC streaming for typed service-to-service execution and high-trust tensor lanes
- webhook intake for controlled external triggers and integrations
- HTTP API inference for synchronous and management-oriented workloads

Without a transport constitution, these paths would become fragmented, redundant, and operationally unsafe. Session 13 prevents that by assigning each transport class a role, a performance envelope, a reliability model, and an observability requirement.

The central principle of this session is:

Every streaming path must have a declared workload purpose, quality class, recovery model, and operational budget.

## 2. Strategic Position

The current repo direction already anticipates a platform-wide streaming fabric:
- the Block Controller is the orchestration hub and already assumes routed live module communication and system event broadcasting fileciteturn14file0L1-L1
- the architecture already defines a SignalR/WebSocket-enabled distributed module network with controller-centered routing and module specialization fileciteturn13file0L1-L1 fileciteturn17file0L1-L1
- the project’s networking rules already define modules as dual-role WebSocket participants with startup registration, heartbeats, and resilient connectivity patterns fileciteturn20file0L1-L1
- the WebSockets and inference skill already establishes bidirectional runtime streaming, typed envelopes, inference endpoints, backpressure using channels, and rate-governed real-time transport behavior fileciteturn19file0L1-L1

Session 13 turns those foundations into streaming law for the entire BCG fabric.

## 3. Session 13 Goals

### Primary Goals
- define the transport hierarchy of the streaming fabric
- assign each transport class a workload role and trust profile
- define stream lifecycle, resume, replay, dedupe, and fan-out rules
- define backpressure and rate-governance requirements
- define how tensors, batches, events, and inference outputs move over different channels
- define the controller’s role in stream governance

### Secondary Goals
- prevent uncontrolled protocol sprawl
- reduce duplicated transport logic across species
- protect hot paths from chatty or mismatched transports
- make live sessions reliable without destabilizing runtime lanes

## 4. Session 13 Deliverables

1. streaming fabric constitution  
2. transport hierarchy and workload matrix  
3. stream lifecycle model  
4. QoS class definitions  
5. replay, resume, and dedupe doctrine  
6. backpressure and fan-out policy  
7. transport observability and telemetry model  
8. operator and session streaming rules  
9. streaming QA and certification gates  
10. acceptance criteria for stream-safe species  

## 5. Streaming Fabric Doctrine

The streaming fabric is the governed system of live and near-live communication across the BCG ecosystem. It includes:
- transport selection
- envelope handling
- connection lifecycle
- stream recovery
- subscription topology
- event fan-out
- tensor and batch streaming semantics
- operator/live session feeds
- external ingress through webhook or API bridges

The streaming fabric is not equivalent to a single library or transport implementation. It is a policy system that coordinates several transport classes.

## 6. Transport Hierarchy

## 6.1 WebSockets and SignalR

### Primary Role
WebSockets and SignalR serve:
- live module-to-controller channels
- operator session feeds
- runtime observability and status broadcasting
- bidirectional interactive sessions
- live user and engineering update participation
- selected inference streams where interactive semantics matter

### Trust Profile
- medium to high trust depending on channel class
- strong identity and role control required
- best suited to session-aware stateful connectivity

### Strengths
- bidirectional communication
- group/topic subscription patterns
- live event fan-out
- operator-friendly semantics
- good fit for session joinability

### Limits
- not the default choice for ultra-strict typed service-to-service execution when gRPC stream semantics are stronger
- must be rate-governed to prevent event storms
- must not become a hidden backdoor around controller transport policy

## 6.2 gRPC Streams

### Primary Role
gRPC streaming serves:
- typed service-to-service tensor flows
- strict block execution streams
- high-trust internal runtime communication
- streaming inference and partial result delivery
- batch container coordination where typed contracts are essential

### Trust Profile
- high trust internal fabric
- strong schema and compatibility discipline required

### Strengths
- strict typed contracts
- efficient service-to-service transport
- good fit for bounded internal streaming lanes
- well aligned with protobuf and tensor contract governance

### Limits
- less natural than SignalR for human-facing operator session experiences
- should not be exposed as a casual public-facing transport without explicit gateway policy

## 6.3 Webhooks

### Primary Role
Webhooks serve:
- controlled external triggers
- event ingress from third-party systems
- compatibility bridges for systems that do not support persistent fabric participation
- low-frequency, event-driven intake

### Trust Profile
- lower trust until validated
- must pass through admission, identity, and DataEvolution or transport normalization rules

### Strengths
- easy external integration
- useful for asynchronous triggers
- clear fit for external intake

### Limits
- not suitable as the primary internal streaming backbone
- not suitable as the sole source of truth for stateful live sessions
- retries and idempotency must be explicit

## 6.4 HTTP API Inference

### Primary Role
HTTP serves:
- synchronous inference
- compatibility endpoints
- admin and management calls
- bounded request-response inference for clients that do not need persistent streams

### Trust Profile
- variable, depending on endpoint class
- should remain tightly scoped

### Strengths
- broad compatibility
- simple request-response model
- appropriate for management and bounded inference flows

### Limits
- not suitable for high-volume live event fan-out
- not suitable for long-horizon operator sessions
- not ideal for internal high-frequency tensor streaming when other transports are available

## 7. Workload-to-Transport Matrix

### Control Plane Events
Preferred transport:
- WebSocket/SignalR for live status and broadcast
- HTTP for bounded management actions

### Internal Typed Execution
Preferred transport:
- gRPC unary and gRPC streaming

### Interactive Operator Sessions
Preferred transport:
- WebSocket/SignalR

### External Trigger Intake
Preferred transport:
- webhooks or HTTP API ingress

### Public Synchronous Inference
Preferred transport:
- HTTP API inference

### Internal Partial Result Streaming
Preferred transport:
- gRPC streams
- WebSocket only where session semantics dominate

### Bulk Fan-Out Notifications
Preferred transport:
- WebSocket/SignalR groups or controller broadcast channels with strict filtering

### Tensor and Batch Streaming
Preferred transport:
- gRPC streams for typed internal lanes
- WebSocket only for observability, session playback, or explicitly declared interactive cases

## 8. Stream Lifecycle Model

Every stream must progress through declared lifecycle phases.

1. Intent Declared  
2. Admission Checked  
3. Connection Established  
4. Subscription Bound  
5. Stream Active  
6. Heartbeat or Liveness Maintained  
7. Backpressure Managed  
8. Resume or Drain Decision  
9. Stream Closed  
10. Replay or Archive Reference Published if applicable  

### Intent Declared
The stream purpose, topic class, or execution function must be known before traffic begins.

### Admission Checked
Identity, role, protocol class, and policy rules are validated.

### Connection Established
Transport channel is opened and registered.

### Subscription Bound
The stream is bound to topics, routes, or execution lanes.

### Stream Active
Live traffic flows under QoS policy.

### Liveness Maintained
Heartbeat, ping, or lease semantics preserve freshness and routeability.

### Backpressure Managed
The stream must obey bounded buffer and overflow rules.

### Resume or Drain Decision
If disruption or cutover occurs, stream behavior must follow declared recovery law.

### Stream Closed
Termination must be explicit and observable.

### Replay or Archive Reference Published
If replayable, the stream must leave durable reference metadata.

## 9. Quality of Service Classes

## QoS-A: Critical Control Streams
Examples:
- controller-to-species command lanes
- lifecycle events during drain or cutover
- emergency control streams

Rules:
- highest trust
- strict identity
- low tolerance for stale or duplicated control messages
- must be observable and recoverable

## QoS-B: Typed Execution Streams
Examples:
- block execution partials
- tensor streaming between internal species
- ML Runtime partial inference outputs

Rules:
- strict schema discipline
- typed envelopes
- bounded latency budgets
- resume or restart semantics must be declared

## QoS-C: Operator and Session Streams
Examples:
- live dashboards
- session event feeds
- engineering update sessions

Rules:
- filtered fan-out
- rate-governed
- degraded detail mode allowed under pressure
- must never compromise control lanes

## QoS-D: External Intake Streams
Examples:
- webhook-driven event intake
- public or partner-origin event bridges

Rules:
- admission hardened
- idempotency required where side effects may repeat
- may enter DataEvolution or intake normalization before becoming trusted fabric events

## 10. Replay, Resume, and Dedupe Doctrine

## 10.1 Replay

Replay is allowed only for streams that declare replayability. Replayable streams must preserve:
- source identity
- event ordering semantics where required
- checkpoint or offset references
- lineage or trace references when data affects tensor or execution state

Replay is especially important for:
- operator forensic sessions
- DataEvolution intake chains
- transformation audit trails
- selected model and tensor-serving diagnostics

## 10.2 Resume

A resumable stream must define:
- resume token or offset strategy
- freshness limits
- duplicate handling rules
- gap detection rules
- whether replay occurs before resume or resume is direct

Not every stream is resumable. Some are restart-only by design.

## 10.3 Dedupe

Dedupe is required where transport retries or reconnect behavior may duplicate delivery. Dedupe policy must define:
- dedupe key
- dedupe window
- side-effect sensitivity
- whether dedupe occurs at producer, router, or consumer boundaries

Critical control and mutation-causing streams require stricter dedupe than observational streams.

## 11. Fan-Out and Subscription Law

## 11.1 Subscription Classes
- direct point-to-point subscription
- topic subscription
- group or broadcast subscription
- session-scoped subscription
- replay-scoped subscription

## 11.2 Fan-Out Rules
- fan-out must be filtered, not blind
- controller broadcast must remain governed and topic-aware
- no uncontrolled “send to all” behavior on high-frequency event classes
- species may subscribe only to approved topic classes
- operator sessions must not automatically inherit all fabric topics

## 11.3 Subscription Safety
- subscriptions must be observable
- dynamic subscription changes must be auditable where privileged
- subscription overload may trigger degradation or forced filtering
- abandoned subscriptions must be cleaned up explicitly

## 12. Backpressure and Flow Control

The streaming fabric must never assume infinite consumer capacity. Backpressure is mandatory.

### Backpressure Rules
- all hot streaming paths must use bounded buffers or equivalent controls
- overflow behavior must be declared: wait, drop-oldest, drop-newest, reject, or escalate
- control-plane streams should prefer rejection or escalation over silent loss
- observational streams may degrade detail before collapsing the lane
- streaming consumers must declare maximum sustainable rate class

### Overflow Classes
- critical overflow
- execution overflow
- observability overflow
- intake overflow

Each overflow class must define whether work is retried, dropped, replayed, or escalated.

## 13. Streaming and Tensor/Batch Semantics

## 13.1 Tensor Streaming
Tensors streamed internally must remain:
- typed
- traceable
- shape-disciplined
- bounded by lane policy

Large tensors should not be naively sprayed over session-facing transports. Instead:
- session channels may receive summaries, IDs, previews, or references
- internal typed streams may move full payloads if budgeted
- large payload references may be externalized through governed storage references

## 13.2 Batch Streaming
Batch containers may expose:
- acceptance
- progress
- partial completion
- failure
- completion and artifact/reference outcomes

Batch streams must not block route governance. Batch progress should be resumable or reconstructable when required by policy.

## 14. Controller Authority Over Streaming

The Block Controller governs the streaming fabric at the policy layer.

The controller governs:
- which topic classes exist
- who may publish and subscribe
- which streams are routeable
- which transport class is allowed for a workload
- protection of control lanes during overload
- degraded streaming mode activation
- session joinability and privilege enforcement
- revocation of abusive or stale stream participants

No species may create hidden durable streaming lanes that bypass controller policy for protected workload classes.

## 15. Session Joinability and Human-Facing Streams

Human-facing streams must remain a governed subset of the fabric.

Allowed session outputs include:
- live health summaries
- filtered route maps
- rollout and drain events
- tensor lineage summaries
- bounded partial inference views
- shell outputs within privilege scope

Forbidden defaults include:
- unrestricted raw internal tensor firehose
- uncontrolled full-fabric event mirror
- implicit access to privileged control channels
- long-lived ghost subscriptions with no identity enforcement

## 16. Security and Trust

Streaming trust rules:
- every stream requires authenticated identity or approved anonymous policy for narrow public lanes
- topic access must be role-bound
- external intake streams are untrusted until normalized and admitted
- internal typed streams must preserve contract version awareness
- replay and resume tokens must be treated as sensitive control objects where they expose state

## 17. Observability Requirements

The streaming fabric must emit enough telemetry to answer:
- which transport class carried a workload
- why that transport was selected
- who published and who subscribed
- whether the stream is active, degraded, draining, resumed, replayed, or failed
- whether the stream breached rate or buffer budgets
- whether events were duplicated, dropped, replayed, or backpressured
- which session or operator was attached
- whether transport selection changed during runtime

### Required Signals
- stream opened
- stream admitted
- subscription bound
- publish accepted
- publish rejected
- buffer pressure raised
- degraded mode entered
- replay started
- replay completed
- resume started
- resume completed
- duplicate detected
- stream drained
- stream closed
- stream revoked

## 18. Failure Model

## 18.1 Connection Loss
If a stream connection breaks:
- reconnect policy must be transport-class specific
- resume or replay path must be explicit
- stale subscriptions must not remain indefinitely active

## 18.2 Overload
If a stream overloads:
- priority protection applies
- operator/session detail may degrade first
- execution or control lanes may reject new load rather than silently corrupt behavior

## 18.3 Duplicate Delivery
If duplicate messages appear:
- dedupe policy applies according to stream class
- mutation-sensitive consumers must reject unsafe duplicates

## 18.4 Replay Gap
If replay or resume cannot bridge a gap:
- the gap must become visible
- the stream may downgrade to restart-only or failure state depending on policy
- operators must not be given false continuity

## 18.5 Transport Misfit
If the chosen transport is wrong for the workload:
- controller policy should redirect or reject the lane
- ad hoc protocol fallback must not become permanent shadow architecture

## 19. QA and Certification Gates

No species may claim Session 13 compliance without:
1. declared transport classes for its workload types  
2. stream lifecycle implementation notes  
3. QoS class assignment  
4. replay/resume/dedupe policy if applicable  
5. backpressure and overflow behavior  
6. observability coverage for stream state changes  
7. load tests for its major stream lanes  
8. failure injection for disconnect or overload  
9. operator/session filtering rules if human-facing streams exist  

## 20. Acceptance Criteria

Session 13 is complete only if:
- each transport class has a declared workload purpose
- stream lifecycle is defined
- QoS classes exist
- replay, resume, and dedupe doctrine is explicit
- fan-out and subscription safety is defined
- backpressure rules are declared
- tensor and batch streaming semantics are governed
- controller authority over streaming is explicit
- observability and QA gates are declared

## 21. Session 13 Final Statement

Session 13 makes the BCG fabric truly live. WebSockets and SignalR become the governed session and event plane. gRPC streams become the typed internal execution plane. Webhooks become controlled external intake bridges. HTTP remains the bounded synchronous and management plane. The result is not protocol chaos, but a layered streaming constitution in which every live path has a role, a budget, a trust class, and a recovery model.
