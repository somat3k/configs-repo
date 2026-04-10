# Session 12 Extended Document
## Dynamic Ports, Network Masking, and Discovery

## 1. Session Purpose

Session 12 defines the network identity constitution of the BCG ecosystem. Its purpose is to make module connectivity relocatable, environment-aware, and future-proof without breaking the control fabric. This session governs how species are discovered, how ports are assigned or abstracted, how network addresses are masked from higher layers, and how the Block Controller preserves route stability even when infrastructure changes underneath it.

The central principle of this session is:

A species must remain addressable by identity and capability first, and by raw endpoint details only through governed discovery layers.

This means modules may move, scale, rotate ports, or exist in different environments, but the BCG fabric must still route work safely. Dynamic networking cannot become randomness. It must become controlled indirection.

## 2. Strategic Position

The current repo direction already establishes several foundations for this session:

- the Block Controller operates as the orchestration hub for registration, routing, and lifecycle control fileciteturn14file0L1-L1
- the network model already assumes distinct module nodes with HTTP and WebSocket presence, startup registration, heartbeat discipline, and service discovery patterns across development and production fileciteturn20file0L1-L1
- the project already identifies a dedicated network module family including runtime, subscription management, container tracking, and network mask responsibilities fileciteturn16file0L1-L1
- the broader architecture remains a controller-centered distributed system with module topology and dynamic growth as a core direction fileciteturn13file0L1-L1 fileciteturn17file0L1-L1

Session 12 converts those foundations into explicit runtime law.

## 3. Session 12 Goals

### Primary Goals
- define how modules are identified independently from physical network placement
- define dynamic port governance without sacrificing route predictability
- define network masking as a formal abstraction layer
- define service discovery and endpoint freshness rules
- define environment-aware routing across local, dev, stage, and prod
- define relocation, replica growth, and network change safety under controller authority

### Secondary Goals
- reduce hardcoded address dependence
- make module scaling and replacement safer
- allow future multi-cluster or hybrid deployment evolution
- prevent stale endpoint drift and untracked route failures

## 4. Session 12 Deliverables

1. network identity constitution  
2. endpoint abstraction model  
3. dynamic port allocation rules  
4. network mask doctrine  
5. discovery and resolution protocol  
6. endpoint freshness and TTL rules  
7. environment routing matrix  
8. relocation and replica admission policy  
9. observability and QA gates for networking  
10. acceptance criteria for discovery-safe species  

## 5. Network Identity Doctrine

## 5.1 Identity Before Address

Every module species must have at least four distinct identity layers:

1. Species Identity  
   The stable logical type of the module, such as Block Controller, DataEvolution, TensorTrainer, ML Runtime, or another species.

2. Instance Identity  
   The unique runtime instance identity for a concrete process or container generation.

3. Capability Identity  
   The declared functional scope that determines what kinds of work the instance may receive.

4. Endpoint Identity  
   The currently active network coordinates through which the instance can be reached.

The system must never treat endpoint identity as the primary truth of a species. Endpoints are mutable runtime facts. Species and capability identity are governing facts.

## 5.2 Endpoint Classes

### Canonical Endpoint
The endpoint representation used internally by discovery and controller routing.

### Public Management Endpoint
The externally visible endpoint for health, admin, or user-facing access.

### Internal Fabric Endpoint
The trusted internal address used for service-to-service traffic.

### Streaming Endpoint
The endpoint class for long-lived WebSocket, SignalR, or gRPC stream traffic.

### Ephemeral Replacement Endpoint
A temporary address used during warm-up, validation, or cutover phases.

## 6. Dynamic Port Doctrine

## 6.1 Why Dynamic Ports Exist

Dynamic ports are allowed for:
- local development flexibility
- multi-instance scaling without collision
- rolling replacement generations
- future container and cluster scheduling freedom
- temporary warm-up lanes for replacement species

Dynamic ports are not an excuse for uncontrolled networking. They must be abstracted behind discovery and masking layers.

## 6.2 Port Classes

### Reserved Static Ports
Well-known default ports used as baseline conventions where stability is beneficial.

### Dynamic Assigned Ports
Ports assigned at runtime by orchestrators, container schedulers, or test harnesses.

### Masked Service Ports
Stable logical service ports exposed through gateway or discovery indirection even if backing ports change.

### Session Ports
Ports or temporary channels used for operator or engineering update sessions under bounded policy.

## 6.3 Port Governance Rules
- every port assignment must be discoverable
- every runtime endpoint must declare protocol and trust class
- no routeable instance may rely on undocumented or hidden ports
- a port change must produce a discovery update event
- controller routeability must depend on validated endpoint state, not on historical guesses

## 7. Network Mask Doctrine

## 7.1 Purpose of the Network Mask

The network mask is the indirection and normalization layer between:
- what a species is
- where that species currently runs
- how clients or peer species should reach it

The network mask prevents higher layers from binding themselves directly to unstable infrastructure details.

## 7.2 Responsibilities of the Network Mask
- maintain logical-to-physical endpoint mapping
- support environment-aware address resolution
- normalize internal versus external addresses
- preserve stable naming during dynamic relocation
- hide replica churn from consuming species where possible
- expose freshness, validity, and confidence of endpoint knowledge

## 7.3 Network Mask Rules
- mask entries must be versioned
- stale entries must expire or be invalidated
- mask updates must be observable
- conflicting endpoint claims must trigger controller-visible arbitration
- mask resolution must never silently downgrade trust class

## 8. Discovery Protocol

## 8.1 Discovery Sources

The platform may resolve endpoint knowledge from:
- startup registration
- controller-maintained module registry
- network mask records
- orchestrator or scheduler signals
- health and heartbeat confirmations
- environment variables and service discovery injectors in development contexts

## 8.2 Discovery Phases

### Phase 1 — Registration Discovery
Species registers declared endpoints and capabilities at startup.

### Phase 2 — Validation Discovery
Controller or discovery service validates reachability, protocol class, and readiness.

### Phase 3 — Routeability Discovery
Validated endpoints become eligible for routing.

### Phase 4 — Refresh Discovery
Heartbeat, lease renewal, or explicit updates keep endpoint knowledge fresh.

### Phase 5 — Relocation Discovery
When an instance moves, the discovery layer publishes change and old routes are retired or drained.

## 8.3 Discovery Rules
- an endpoint is not routeable until validated
- a validated endpoint is not permanent truth; freshness must be maintained
- discovery must separate declaration from confirmation
- routeability must be revoked when freshness expires or health confidence collapses
- duplicate identity claims must trigger arbitration and not blind overwrite

## 9. Endpoint Freshness and TTL Law

Every endpoint record must declare:
- last confirmed timestamp
- freshness TTL
- protocol class
- health confidence
- environment scope
- owning species and instance identity
- generation or rollout relation if relevant

### Freshness Classes

#### Short TTL
Used for rapidly changing or experimental species, ephemeral replacements, or unstable environments.

#### Medium TTL
Used for ordinary dynamic module instances under regular heartbeat discipline.

#### Long TTL
Used for stable logical service aliases or masked service identities, not for raw instance claims.

### Freshness Rules
- stale raw endpoints must not remain silently routeable
- expired endpoints may remain historically visible but not active
- freshness thresholds must be stricter for streaming and control-plane endpoints than for passive metadata references
- old endpoints may remain in drain state during cutover but must be marked explicitly

## 10. Environment Routing Matrix

## 10.1 Local Development
- flexible discovery allowed
- dynamic ports common
- developer overrides acceptable
- route safety still required
- no hidden local shortcuts allowed in committed contracts

## 10.2 Shared Development
- dynamic ports allowed with stronger discovery enforcement
- conflicts must be visible
- controller registry and mask must remain the source of route truth

## 10.3 Staging
- address discipline must resemble production
- masked logical names preferred
- relocation and replacement rehearsals mandatory

## 10.4 Production
- routes must resolve through trusted discovery and masking layers
- direct hardcoded internal endpoints are forbidden except under emergency break-glass policy
- endpoint trust class and environment provenance must be explicit
- all route changes are observable events

## 11. Relocation and Replica Governance

## 11.1 Relocation
Relocation is the movement of a species instance from one network coordinate to another while preserving logical identity or replacing it with controlled generation change.

Relocation rules:
- controller must be aware of relocation intent or observe relocation event
- old and new endpoints must not remain ambiguously active
- route weights and readiness must govern transition
- relocation must preserve audit and trace continuity

## 11.2 Replica Admission
When a species scales out:
- each replica must register its instance identity
- capabilities may be identical or subset-scoped
- route selection may use health, load, region, or specialization
- masked logical service identity may represent the replica set while preserving per-instance observability

## 11.3 Replica Retirement
- retiring replicas must drain or explicitly reject new work
- stale replicas must not remain discoverable beyond freshness horizon
- replica retirement must not break logical service identity

## 12. Controller Authority Over Discovery

The Block Controller remains the governing routing authority. Session 12 extends that role into network identity arbitration.

The controller governs:
- acceptance of endpoint declarations
- confirmation of routeability
- revocation of stale routeability
- relocation visibility
- replica set participation
- environment-aware routing policy
- conflict resolution when endpoint claims disagree

No species may self-promote a new endpoint into full production routeability without controller acknowledgment or approved autonomous policy.

## 13. Transport Alignment

Dynamic networking must remain compatible with the transport constitution.

### HTTP and Management Endpoints
- suitable for health, admin, registration, and certain synchronous control paths

### WebSocket and SignalR Endpoints
- suitable for live eventing, session joinability, controller channels, and operator feeds

### gRPC and Streaming Endpoints
- suitable for typed low-latency service-to-service interactions and stream-capable species

### Webhooks
- suitable for controlled intake from external systems, never as sole authority for internal endpoint discovery

Transport class must be part of discovery metadata, not inferred by guesswork.

## 14. Security and Trust Boundaries

Dynamic discovery must not weaken trust.

Rules:
- endpoint declarations require authenticated species identity
- trust class of internal versus external endpoints must be explicit
- environment crossing must be deliberate and audited
- masked external exposure must not leak privileged internal routes
- operator-visible endpoints may differ from fabric endpoints and must remain separately governed

## 15. Observability Requirements

The network layer must emit enough information to answer:
- which species and instance owns an endpoint
- whether the endpoint is declared, validated, routeable, stale, draining, or retired
- which mask entry resolved a given route
- whether route failure was due to stale knowledge, relocation, trust mismatch, or health collapse
- whether environment rules changed the resolved endpoint
- which generation or replica received the work

### Required Signals
- endpoint declared
- endpoint validated
- endpoint freshness renewed
- endpoint stale
- endpoint revoked
- relocation started
- relocation completed
- replica admitted
- replica retired
- mask entry created
- mask entry updated
- mask entry invalidated
- route resolution succeeded
- route resolution failed

## 16. Failure Model

## 16.1 Stale Endpoint Failure
If an endpoint remains in registry after relocation or crash:
- routeability must be revoked on freshness expiry
- controller must publish stale endpoint event
- fallback resolution may use masked logical identity if healthy replicas exist

## 16.2 Split Claim Failure
If two instances claim conflicting ownership:
- arbitration required
- routeability may be suspended for contested identity
- operator-visible incident must be raised

## 16.3 Discovery Service Degradation
If discovery freshness cannot be maintained:
- last known good routes may be used only within bounded policy
- high-risk route classes may be suspended
- production promotions and cutovers should freeze

## 16.4 Mask Drift Failure
If network mask entries diverge from controller truth:
- controller truth prevails
- mask must be reconciled or invalidated
- downstream consumers must not continue indefinite blind trust

## 17. QA and Certification Gates

No species may claim Session 12 compliance without:
1. declared identity layers  
2. endpoint metadata schema  
3. freshness TTL policy  
4. relocation behavior notes  
5. replica admission and retirement notes  
6. controller acknowledgment path  
7. stale endpoint test coverage  
8. mask invalidation or refresh tests  
9. environment routing documentation  
10. observability coverage for endpoint lifecycle  

## 18. Acceptance Criteria

Session 12 is complete only if:
- species identity is formally separated from raw endpoint identity
- dynamic port rules are explicit
- network masking is formally defined
- discovery phases and validation rules exist
- freshness and TTL rules exist
- relocation and replica governance are defined
- controller authority over endpoint arbitration is explicit
- transport alignment remains intact
- observability and QA gates are declared

## 19. Session 12 Final Statement

Session 12 makes the BCG network fabric relocatable without becoming unstable. A species is no longer defined by a hardcoded address. It is defined by governed identity, validated capability, and discovery-backed reachability under controller authority. Dynamic ports become safe through masking. Relocation becomes manageable through freshness and arbitration. Replica growth becomes deliberate through routeable identity rather than blind fan-out. The result is a network layer that can evolve with the platform instead of trapping it.
