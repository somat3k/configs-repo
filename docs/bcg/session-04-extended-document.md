# Session 04 Extended Document
## Protobuf, Envelope, and Transport Unification Constitution

## 1. Session Purpose

Session 04 establishes the transport constitution for the BCG ecosystem. Its purpose is to eliminate ambiguity across inter-module communication by defining one production-grade transport law covering protobuf contracts, envelope governance, request/response semantics, streaming semantics, compatibility, versioning, performance classes, retry policy, and transport observability.

This session converts communication from an implementation detail into a governed system capability.

The result of Session 04 is not merely a set of `.proto` files. It is a complete transport discipline for a live multi-module runtime where every message can be classified, routed, validated, versioned, traced, and audited.

## 2. Strategic Position

The current repo direction already requires every module to expose both HTTP and WebSocket interfaces, register with the Block Controller, and communicate through a shared envelope protocol with versioned metadata and runtime identity fields. The architecture skills also define an event-driven microservice topology with a central controller and typed envelope messages as the baseline for inter-module communication.

Session 04 does not replace those principles. It hardens them by introducing a formal transport stack with explicit class boundaries:

- protobuf/gRPC for strongly typed RPC and authoritative structured lanes
- envelope protocol for cross-cutting identity, routing, session, and trace metadata
- WebSocket and SignalR for live eventing, streaming, subscription, and operator/session control
- webhook ingress for external trigger intake
- HTTP for management, compatibility, and controlled sync APIs
- binary-first performance paths for hot routing and heavy event volumes

## 3. Session 04 Goals

### Primary Goals
- unify all transport lanes under one constitution
- define protobuf as the main typed contract language for machine-governed lanes
- align the envelope model with protobuf and runtime metadata
- define versioning and compatibility rules
- define message classes, ownership, and selection policy
- define validation, retry, timeout, deduplication, and failure semantics

### Secondary Goals
- support DataEvolution intake paths without transport chaos
- support TensorTrainer and ML Runtime promotion without schema drift
- support dynamic modules and future species without protocol collapse
- support live runtime updates with transport continuity

## 4. Scope of Session 04

This session covers:

- protobuf schema governance
- envelope structure
- request/response and streaming contract classes
- batch transport model
- topic subscription model
- compatibility and migration rules
- serialization standards
- routing metadata requirements
- transport observability and QA

This session does not fully implement all future schemas. It defines the legal framework every future schema must obey.

## 5. Transport Doctrine

## 5.1 Foundational Rule

No inter-module communication path may exist in production unless it has:

- a declared owner
- a declared transport type
- a declared schema version
- a declared compatibility rule
- a declared timeout and retry rule
- a declared observability contract
- a declared failure behavior

## 5.2 Transport Classes

### Class A — Authoritative Machine Contracts
Used for:
- module registration
- capability reporting
- route execution
- tensor transformation
- batch execution
- health state streaming
- model activation and promotion
- stateful runtime commands

Transport:
- protobuf + gRPC

Requirements:
- explicit schemas
- compatibility guarantees
- strict validation
- deterministic serialization
- contract testing mandatory

### Class B — Live Event and Stream Fabric
Used for:
- operator session updates
- runtime observability feeds
- partial outputs
- subscription updates
- notifications and live telemetry
- human-visible live orchestration

Transport:
- WebSocket / SignalR
- optional protobuf-framed binary subprotocol in future

Requirements:
- envelope metadata always attached
- bounded rate and backpressure rules
- resumability or replay policy where needed
- clear event typing

### Class C — External Trigger and Compatibility Ingress
Used for:
- webhooks
- external partner integrations
- controlled REST ingress
- backward-compatible management endpoints

Transport:
- HTTP/JSON or typed HTTP with schema validation

Requirements:
- strict intake validation
- conversion through DataEvolution or transport adapters where appropriate
- envelope enrichment before internal routing

### Class D — Artifact and Large Object References
Used for:
- large tensor payload references
- model artifacts
- large dataset references
- snapshots and replay packages

Transport:
- transport carries references, not whole artifacts, beyond threshold
- object location managed through Redis/Postgres/IPFS policy

Requirements:
- integrity fields
- retrieval authorization
- lineage linkage

## 6. Envelope Constitution

## 6.1 Envelope Role

The envelope is not a payload substitute. It is the cross-cutting transport shell that carries the identity and governance fields required for routing, tracing, ownership, and operational control.

The project already defines envelope-driven communication as a core architecture rule. Session 04 formalizes the envelope as a mandatory runtime wrapper around transport activity where applicable.

## 6.2 Mandatory Envelope Fields

Every authoritative or streaming message must be representable with the following metadata set:

- `type`
- `version`
- `session_id`
- `module_id`
- `timestamp`
- `trace_id`
- `task_id`
- `block_id` where block-scoped
- `payload_kind`
- `payload_schema`
- `routing_scope`
- `priority`
- `correlation_id`
- `causation_id`
- `target_module` or topic when applicable
- `transport_class`

Optional extended fields may include:
- `tenant_id`
- `operator_id`
- `artifact_ref`
- `tensor_ids`
- `signature_name`
- `compression`
- `content_hash`

## 6.3 Envelope Invariants

- the envelope must be parseable before the payload body is fully materialized
- envelope version and payload schema version are separate concerns
- envelopes may carry protobuf payloads, JSON payloads, stream fragments, or artifact references
- no envelope may omit type and version
- correlation and causation must be preserved across routed chains
- all envelopes must be trace-compatible with the Block Controller runtime

## 7. Protobuf Constitution

## 7.1 Protobuf Role

Protobuf is the universal schema language for machine-governed transport lanes. It defines the durable wire contracts that support registration, execution, transformation, orchestration, and high-confidence module interactions.

## 7.2 Protobuf Package Ownership

The following package domains are reserved:

- `bcg.tensor`
- `bcg.block`
- `bcg.module`
- `bcg.orchestrator`
- `bcg.transform`
- `bcg.runtime`
- `bcg.training`
- `bcg.observability`
- `bcg.storage`
- `bcg.session`

Each package must have:
- owning module or governance group
- compatibility notes
- approved field evolution policy
- schema review gate

## 7.3 Field Governance Rules

- field numbers are permanent once released
- deleted fields must be reserved, never silently reused
- enum values must be forward-safe
- required semantics must be enforced by validation, not unsupported proto behavior
- unknown fields must be tolerated on newer consumers where possible
- field names should optimize clarity over brevity

## 7.4 Message Design Classes

### Control Messages
Examples:
- RegisterModule
- RegisterAck
- HealthEvent
- RouteAck
- DrainRequest
- CapabilityUpdate

### Execution Messages
Examples:
- BlockRequest
- BlockResponse
- BatchRequest
- BatchResponse
- StreamExecuteFragment

### Tensor Messages
Examples:
- BcgTensorProto
- TensorShape
- TensorTransformRequest
- TensorTransformResponse
- TensorReference

### Session Messages
Examples:
- SessionJoinRequest
- SessionStateEvent
- ShellCommandEnvelope
- OperatorControlAck

### Artifact Messages
Examples:
- ModelArtifactRef
- DatasetArtifactRef
- SnapshotRef
- IntegrityManifest

## 8. Compatibility and Versioning Law

## 8.1 Version Types

Three versions must be tracked independently:

1. envelope version
2. payload schema version
3. module implementation version

These may move separately.

## 8.2 Compatibility Modes

### Backward-Compatible Change
Examples:
- adding optional fields
- adding tolerable enum values
- adding new message types that old consumers can ignore

### Forward-Compatible Tolerable Change
Examples:
- consumer ignoring future-added optional fields
- topic listeners accepting event types outside their interest set

### Breaking Change
Examples:
- field semantic change
- field removal without reservation policy
- meaning change in existing enum values
- required operational dependency on newly introduced fields without fallback

Breaking changes require:
- explicit major schema step
- migration document
- rollback path
- dual-read or compatibility shim where relevant

## 8.3 Schema Promotion Policy

A schema cannot be promoted to production-grade unless it has:
- protobuf lint pass
- semantic review
- contract tests
- compatibility notes
- failure mode description
- representative sample payloads

## 9. Transport Selection Matrix

## 9.1 gRPC / Protobuf

Use when:
- request/response correctness matters more than loose eventing
- the path is authoritative
- compatibility and typing matter strongly
- the Block Controller is governing routing or state transitions
- tensor execution or transformation contracts are involved

Do not use when:
- the path is broadcast-heavy operator telemetry with weak coupling
- the path requires browser-first loose event consumption without specialized clients

## 9.2 WebSocket / SignalR

Use when:
- live subscriptions are required
- partial outputs must be pushed continuously
- operator dashboards need updates
- session-driven runtime interactions occur
- event fan-out is required

Do not use when:
- the path requires strict unary RPC semantics and strong typed acks as the dominant behavior

## 9.3 HTTP / Webhook

Use when:
- integrating external systems
- receiving third-party triggers
- serving management or compatibility endpoints
- supporting controlled synchronous APIs

Do not use when:
- the path is a high-frequency internal runtime lane better suited to gRPC or streaming

## 10. Serialization Standards

## 10.1 Production Serialization Rule

For hot paths, binary-first serialization is the target direction. The current repo instructions already emphasize MessagePack for production wire serialization in performance-heavy lanes, while JSON remains acceptable for human-readable configuration and compatible intake cases.

Session 04 therefore sets this hierarchy:

- protobuf for authoritative typed RPC
- binary event framing for hot streaming lanes where supported
- JSON only for external compatibility, management APIs, and human-centered configuration or diagnostics

## 10.2 Compression Rules

Compression may be used only when:
- payload size exceeds threshold
- latency budget still holds
- receiver advertises support
- observability records compression state

Compression is prohibited for tiny hot-path payloads where CPU overhead harms latency.

## 11. Timeout, Retry, and Delivery Semantics

## 11.1 Timeout Classes

### Control-plane timeout
Short, deterministic, low tolerance for ambiguity.

### Execution timeout
Depends on workload class, tensor size, and block type.

### Stream idle timeout
Protects long-lived streams without active data.

### Artifact retrieval timeout
Longer budget, but must remain observable and cancellable.

## 11.2 Retry Rules

Retries require classification:
- safe idempotent retry
- retry with dedupe token
- non-retriable mutation
- operator-confirmed retry

No transport path may silently retry state-changing commands without dedupe or idempotency strategy.

## 11.3 Delivery Guarantees

The system supports the following explicit semantics by route:
- at-most-once
- at-least-once with dedupe
- ordered stream per topic or channel
- best-effort broadcast

Every schema or route must declare which it uses.

## 12. Validation and Admission Rules

## 12.1 Intake Validation

Every ingress path must validate:
- envelope structure
- transport class expectations
- schema version support
- payload size and threshold
- content integrity markers where required
- module authorization and scope
- topic or destination legitimacy

## 12.2 Internal Admission

A message admitted into the internal fabric must have:
- normalized identifiers
- trace metadata
- validated payload shape or schema
- explicit route class
- observability tags

Data that does not conform must either:
- be rejected
- be quarantined
- be sent through DataEvolution
- be down-converted through a declared compatibility adapter

## 13. Block Controller Responsibilities Under Session 04

The Block Controller now becomes the governing admission and route authority for transport discipline. It must be able to:

- verify module registration contract compliance
- verify schema support and version claims
- reject unknown or incompatible route attempts
- choose a transport lane where route policy allows controller mediation
- emit transport governance events
- observe compatibility mismatches and drift
- support safe degradation for unavailable modules

## 14. DataEvolution and Tensor Alignment

## 14.1 DataEvolution Alignment

DataEvolution is the mandatory bridge for non-native input classes. It must support:
- source data intake
- schema inference or mapping
- normalization into BCG-native payloads
- tensor or graph-ready uplift
- lineage-preserving conversion
- compatibility adaptation for legacy senders

## 14.2 Tensor Alignment

Transport and tensors must align on these rules:
- small tensors may travel inline
- medium tensors may travel inline if latency budget and memory budget allow
- large tensors travel by reference beyond threshold
- tensor lineage must survive any transport change
- transport must preserve tensor IDs, trace IDs, and origin markers

## 15. Storage Routing Thresholds for Transport

Aligned with the repo storage discipline across PostgreSQL, Redis, and IPFS, Session 04 sets the policy direction:

- Redis: hot transport correlation state, short-lived routing context, cached message fragments where needed
- PostgreSQL: authoritative route history, compatibility metadata, audit trails, schema registration metadata
- IPFS or artifact storage: large tensor references, model artifacts, replay packages, large snapshots

Large payloads must not be naively pushed through ordinary control-plane routes when reference transport is the correct path.

## 16. Observability Requirements

Every transport class must emit:
- route start and finish
- sender and target identity
- schema name and version
- compatibility result
- validation result
- transport class
- payload size bucket
- retry count
- dedupe status if applicable
- error class if failed
- latency p50/p95/p99 metrics

Streaming lanes must additionally emit:
- connection start and end
- stream idle time
- backpressure signals
- dropped or deferred events
- resume attempts

## 17. QA and Certification Gates

No transport path is production-certified unless it passes:

- schema linting
- contract tests
- compatibility tests
- malformed payload tests
- timeout and cancellation tests
- retry and dedupe tests
- load tests for declared throughput class
- observability verification
- authorization verification for privileged routes

### Required Test Families
- protobuf round-trip tests
- version tolerance tests
- envelope parser tests
- mixed transport integration tests
- route governor integration tests
- stream soak tests
- webhook ingress sanitation tests

## 18. Performance Budget

### Control-plane unary RPC
- target under 50 ms p95 end-to-end for normal routes

### Route admission
- target under 10 ms p95 inside controller-local decision logic

### Streaming first event
- target under 250 ms p95 for standard subscriptions

### Serialization overhead
- must be measured separately from business execution
- hot path serialization must avoid unnecessary allocation

### Failure budget
- malformed external ingress must fail fast
- transport incompatibility must not poison healthy lanes

## 19. Session 04 Deliverables

1. transport constitution document  
2. protobuf package map  
3. envelope law  
4. compatibility and versioning policy  
5. transport selection matrix  
6. validation and admission rules  
7. retry and timeout policy  
8. transport observability checklist  
9. QA and certification matrix  
10. migration note template for future schema changes  

## 20. Session 04 Exit Criteria

Session 04 is complete only if:

- transport classes are defined
- protobuf governance is defined
- envelope governance is defined
- schema versioning rules exist
- compatibility categories are explicit
- retry/timeout semantics are explicit
- validation and admission rules are explicit
- controller transport authority is defined
- DataEvolution has a formal place in intake normalization
- QA gates exist for all transport classes

## 21. Final Statement

Session 04 makes communication a governed production asset. From this point onward, modules are not allowed to invent transport behavior ad hoc. Every serious message path must be classed, typed, versioned, traced, validated, observable, and certifiable. This is the point where the BCG system stops behaving like a collection of loosely connected services and starts behaving like a disciplined runtime fabric with formal transport law.
