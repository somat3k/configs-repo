# Session 18 Extended Document
## Module Species Packs

## 1. Session Purpose

Session 18 defines the species-governance constitution of the BCG ecosystem. Its purpose is to formalize every module as a separate living species with its own charter, behavior, contracts, documentation pack, budgets, operational rules, and evolution path, while still remaining interoperable with the Block Controller and the broader control fabric.

The central principle of this session is:

A module is not just a service. It is a governed species.

This means every module must have:
- a stable identity
- a bounded mission
- explicit contracts
- known dependencies
- known obligations to the Block Controller
- its own operating documents
- its own performance and trust budget
- its own failure and recovery doctrine
- its own roadmap and mutation history

The point of this session is not to fragment the platform. It is to let the platform grow without collapsing into one undocumented organism.

## 2. Strategic Position

The current repo direction already points toward a species-style ecosystem:
- the architecture is controller-centered and distributed, with the Block Controller operating as the orchestration hub and modules acting as separate network nodes fileciteturn13file0L1-L1 fileciteturn14file0L1-L1
- the system architecture skill already frames the platform as event-driven microservices with module topology, bounded module roles, and self-reporting behavior fileciteturn17file0L1-L1
- the repository already includes distinct modules for trader, arbitrager, defi, ml-runtime, data-layer, broker, transactions, network modules, designer, and related infrastructure species fileciteturn13file0L1-L1
- your BCG direction adds further mandatory species such as DataEvolution and TensorTrainer, each requiring independent operation while still communicating with the controller fabric

Session 18 makes this modularity constitutional.

## 3. Session 18 Goals

### Primary Goals
- define what a module species is in the BCG ecosystem
- define the required 20-document living pack for every species
- define inter-species communication rules
- define ownership, stewardship, and upgrade compatibility rules
- define how a species grows independently without breaking the platform
- define how new species are admitted into the control fabric

### Secondary Goals
- prevent undocumented module sprawl
- reduce hidden coupling between species
- make species independently operable and certifiable
- preserve future growth without architectural drift

## 4. Session 18 Deliverables

1. species-governance constitution  
2. species identity and lifecycle doctrine  
3. required 20-document pack definition  
4. inter-species communication doctrine  
5. compatibility and mutation rules  
6. ownership and stewardship matrix  
7. species admission policy  
8. observability and QA requirements for packs  
9. module family taxonomy  
10. acceptance criteria for species-grade modules  

## 5. Species Doctrine

A species in BCG is a module or bounded service organism that:
- has a stable logical identity
- serves a distinct mission
- can run as a standalone server or controlled runtime participant
- communicates through governed transports
- declares its contracts and budgets
- is independently observable and operable
- participates in the BCG fabric through Block Controller governance

A species is not allowed to remain:
- contract-ambiguous
- undocumented
- hidden behind another species without declared dependency
- operationally anonymous
- impossible to certify on its own

## 6. Species Identity Model

Every species must define:

### 6.1 Canonical Identity
The stable name of the species, such as:
- Block Controller
- DataEvolution
- TensorTrainer
- ML Runtime
- Trader
- Arbitrager
- DeFi
- Data Layer
- Broker
- Transactions
- Designer
- Shell
- network-mask
- subscription-manager
- runtime
- container-registry
- ai-hub
- and other admitted species

### 6.2 Species Mission
A one-paragraph production-grade mission statement that defines:
- what it exists to do
- what it does not do
- which other species depend on it
- what kinds of workloads it should accept

### 6.3 Species Boundaries
A formal statement of:
- owned data and state classes
- transport obligations
- runtime obligations
- trust class
- dependencies
- forbidden responsibilities

### 6.4 Species Lifecycle
Each species must define:
- bootstrap behavior
- registration behavior
- warming behavior
- active behavior
- draining behavior
- recovery behavior
- archival or retirement behavior

## 7. Species Families

The ecosystem may group species into families, but each species still remains independently governed.

## 7.1 Control Family
Examples:
- Block Controller
- policy or registry authorities
- trust and identity governance species

## 7.2 Runtime Family
Examples:
- batch executor species
- shell/runtime managers
- kernel runtime species
- ML Runtime

## 7.3 Transformation Family
Examples:
- DataEvolution
- schema mappers
- graph uplift and normalization species

## 7.4 Intelligence Family
Examples:
- TensorTrainer
- inference species
- AI Hub
- model-serving and model-evaluation species

## 7.5 Domain Family
Examples:
- Trader
- Arbitrager
- DeFi
- Transactions
- Broker

## 7.6 Infrastructure Family
Examples:
- data-layer
- network modules
- discovery and mask species
- container-registry
- runtime support species

## 7.7 Interface Family
Examples:
- web-app
- designer
- operator-plane species
- session-facing surfaces

Families help organization, but they do not erase individual species obligations.

## 8. Required 20-Document Living Pack

Every species must maintain a living documentation pack with the following minimum set.

## 8.1 Document 01 — Species Charter
Defines mission, scope, non-goals, success criteria, and relationship to the BCG fabric.

## 8.2 Document 02 — Context and Scope
Defines where the species sits in the ecosystem, which species it depends on, and which workloads it serves.

## 8.3 Document 03 — Architecture Overview
Defines internal architecture, major components, dependencies, transport participation, and runtime topology.

## 8.4 Document 04 — Runtime Contract
Defines lifecycle, registration, health, draining, replacement, and recovery behavior.

## 8.5 Document 05 — Tensor Contract
Defines whether the species produces, consumes, transforms, stores, summarizes, or externalizes tensors, and under which rules.

## 8.6 Document 06 — Protobuf and API Schemas
Defines service contracts, HTTP/gRPC/webhook surfaces, and streaming schemas.

## 8.7 Document 07 — Configuration Specification
Defines required settings, defaults, protected settings, environment overrides, and reload behavior.

## 8.8 Document 08 — Port and Network Map
Defines endpoint classes, discovery participation, masking, environment differences, and routeability notes.

## 8.9 Document 09 — Data Flow and Sequence Diagrams
Defines typical and critical paths through the species.

## 8.10 Document 10 — State Model and Persistence
Defines owned state classes, storage routing, retention, replay, and restoration.

## 8.11 Document 11 — Security Model
Defines identity, authorization, trust class, secrets, artifact trust, and sensitive actions.

## 8.12 Document 12 — Observability and Telemetry
Defines metrics, events, traces, dashboards, and incident evidence.

## 8.13 Document 13 — Performance Budget
Defines latency, throughput, memory, allocation, and degraded-mode expectations.

## 8.14 Document 14 — QA and Test Strategy
Defines test taxonomy, certification level, contract tests, resilience checks, and performance verification.

## 8.15 Document 15 — Deployment and Rollback
Defines rollout path, replacement path, rollback relation, compatibility caveats, and stabilization windows.

## 8.16 Document 16 — Runbook and On-Call Guide
Defines operator procedures, alerts, escalation, and recovery workflows.

## 8.17 Document 17 — Failure Modes and Recovery
Defines expected failure classes, degradation behavior, and post-failure restoration semantics.

## 8.18 Document 18 — Change Log and Migration Notes
Defines structural changes, compatibility changes, migration steps, and deprecations.

## 8.19 Document 19 — Session Progress Ledger
Defines what each session changed for the species, including architecture and operational consequences.

## 8.20 Document 20 — Future Evolution Roadmap
Defines intended expansions, deferred responsibilities, and species-level long-range evolution.

These are living documents. They must be updated as sessions modify the species.

## 9. Inter-Species Communication Doctrine

A species does not communicate with the ecosystem arbitrarily. It communicates through governed lanes.

Every species must declare:
- which transports it uses
- which contracts it publishes
- which contracts it consumes
- what topics or routes it may subscribe to
- what kinds of external intake it permits
- what kinds of controller commands it obeys

### Communication Rules
- no species may depend on undocumented side channels
- no species may require hidden shared memory assumptions across independent runtime boundaries
- every inter-species lane must have contract ownership
- communication purpose must be declared: control, execution, observability, intake, replay, or session
- sensitive or privileged communication must carry trust posture and auditability

## 10. Species-to-Controller Relationship

Every species must define its exact relationship to the Block Controller.

The Controller-facing species contract must answer:
- how the species registers
- what identity and capability it declares
- what health and lifecycle states it reports
- what route classes it accepts
- under what conditions it refuses work
- how it behaves during drain, cutover, and rollback
- how it participates in observability and trust law

No species may treat controller participation as optional if it is part of the BCG control fabric.

## 11. Standalone Operation Doctrine

One of your core requirements is that species should be able to run as standalone servers while still participating in the network. Session 18 formalizes that.

Each species must state:
- how it runs independently
- what minimum dependencies it needs
- whether it can operate in isolated mode
- which features degrade when controller or peer species are absent
- what it exposes when running alone
- how it rejoins the BCG fabric after isolation

Standalone operation must not imply governance bypass. A standalone species still carries:
- contracts
- trust rules
- observability duties
- state rules
- upgrade responsibilities

## 12. Compatibility and Mutation Law

Species evolve over time. That evolution must be governed.

Every species must define:
- version model
- contract compatibility expectations
- schema evolution notes
- supported downgrade or rollback behavior
- mutation impact classes
- deprecation rules

### Mutation Classes
- cosmetic
- non-breaking operational
- non-breaking contract
- performance-sensitive
- behavior-changing
- breaking contract
- migration-required
- retirement or merge event

The more severe the mutation, the stronger the review, documentation, and release gates.

## 13. Ownership and Stewardship Matrix

Every species must have explicit ownership.

The species pack must identify:
- primary steward
- backup steward
- owning team or responsibility class
- release approver class
- trust approver class if relevant
- operational escalation owner

Species with no owner are considered non-production-ready.

## 14. Performance and Budget Responsibility

Each species must carry its own budgets, not borrow them vaguely from the platform.

A species must define:
- its main latency budgets
- throughput expectations
- memory and storage posture
- cache dependence
- degraded-mode behavior
- performance-sensitive interfaces
- benchmark expectations where relevant

The project’s broader performance direction already requires measurable hot paths and bounded producer-consumer patterns fileciteturn24file0L1-L1. Session 18 requires every species pack to localize that into its own budget.

## 15. Species Observability Responsibility

Every species pack must define:
- health signals
- lifecycle signals
- transport and storage signals
- tensor or artifact signals if applicable
- dashboard surface expectations
- forensic evidence notes
- alert ownership

This is required so species remain independently diagnosable, not only understandable in aggregate.

## 16. Species Admission Policy

A new species may enter the BCG ecosystem only if it provides at minimum:
- canonical identity
- mission and boundaries
- controller relationship
- transport contract
- trust class
- storage ownership
- observability baseline
- QA baseline
- steward assignment
- initial living pack

Admission may be:
- experimental
- development-grade
- staging-grade
- production-grade

A species cannot be production-grade by declaration alone.

## 17. Species Interaction Patterns

The platform must allow different species interaction patterns, but each must be declared.

### Common Interaction Patterns
- controller-routed command execution
- service-to-service typed execution
- transformation handoff
- model artifact promotion
- event broadcast
- operator session observation
- replay or forensic reference
- batch container delegation

Each species pack should describe which patterns it uses and which it forbids.

## 18. Species Template Recommendations by Major BCG Species

## 18.1 Block Controller Species Pack
Must emphasize:
- global governance
- route authority
- trust and lifecycle visibility
- observability aggregation
- degraded mode and rollback coordination

## 18.2 DataEvolution Species Pack
Must emphasize:
- intake classes
- schema and lineage
- normalization and graph uplift
- transformation performance and failure semantics

## 18.3 TensorTrainer Species Pack
Must emphasize:
- training workload classes
- compute budgets
- artifact production
- reproducibility and dataset lineage
- promotion handoff to runtime-serving species

## 18.4 ML Runtime Species Pack
Must emphasize:
- artifact activation
- signature-bound serving
- inference lanes
- rollout and rollback
- confidence and drift visibility

## 18.5 UI / Web-App Species Pack
Must emphasize:
- operator-plane behaviors
- role-aware control surfaces
- live session UX
- degraded views
- UI-side observability and audit

## 18.6 Domain Species Packs
Trader, Arbitrager, DeFi, Broker, Transactions, and similar species must emphasize:
- domain mission
- execution lanes
- external dependencies
- risk boundaries
- domain-specific rollback and validation notes

## 19. Failure Model

## 19.1 Undocumented Species Failure
If a species runs but does not carry its pack, the governance model has failed.

## 19.2 Hidden Coupling Failure
If two species depend on implicit undocumented knowledge, species independence has failed.

## 19.3 Ownerless Species Failure
If a species has no steward or no operational owner, production accountability has failed.

## 19.4 Drifted Pack Failure
If the species implementation and the pack disagree materially, the species is considered under-documented and non-compliant.

## 20. QA and Certification Gates

No species may claim Session 18 compliance without:
1. a complete species identity section  
2. a declared controller relationship  
3. a living 20-document pack scaffold  
4. stewardship and ownership declaration  
5. transport and trust baseline  
6. storage and observability baseline  
7. compatibility and mutation notes  
8. admission class definition  
9. session progress ledger strategy  
10. roadmap or future evolution section  

## 21. Acceptance Criteria

Session 18 is complete only if:
- a module is formally defined as a species
- the required 20-document pack is defined
- inter-species communication rules are explicit
- controller relationship rules are explicit
- standalone operation doctrine exists
- compatibility and mutation law exists
- ownership and stewardship requirements are explicit
- species admission policy is defined
- QA and certification expectations for packs are defined

## 22. Session 18 Final Statement

Session 18 turns the BCG ecosystem into an organized biosphere of modules rather than a pile of services. Each species becomes separately intelligible, separately operable, separately certifiable, and separately evolvable. Yet none of them become isolated from the control fabric. They remain governed by the Block Controller, aligned to shared transport and trust law, and responsible for their own living knowledge. This is how the platform scales in complexity without losing coherence.
