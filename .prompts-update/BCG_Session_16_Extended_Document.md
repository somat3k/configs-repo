# Session 16 Extended Document
## UI and Operator Experience

## 1. Session Purpose

Session 16 defines the operator-plane constitution of the BCG ecosystem. Its purpose is to turn the UI from a passive front end into a governed operational instrument for observing, composing, controlling, and evolving the platform. This session establishes how the Blazor + Fluent UI surface becomes the human-facing layer of the control fabric while preserving production safety, role boundaries, and runtime continuity.

The central principle of this session is:

The UI is not decoration. It is a governed operator plane.

This means the interface must allow people to:
- understand the system at runtime
- compose and inspect block graphs
- observe modules, tensors, batches, streams, and artifacts
- participate in live sessions
- perform approved control actions
- diagnose degradation and respond with safe workflows

At the same time, the UI must never become:
- an ungoverned mutation channel
- a hidden bypass around controller authority
- a raw firehose of destabilizing data
- a place where unsafe actions are easier than safe ones

## 2. Strategic Position

The current repo direction already supports this session:
- the architecture defines a Blazor MDI web application as a first-class platform surface, alongside the Block Controller and specialized modules fileciteturn13file0L1-L1
- the Block Controller is already the orchestration hub for lifecycle, routing, heartbeat, and observability aggregation, making it the natural backbone for operator-plane state fileciteturn14file0L1-L1
- the project skills explicitly point toward premium Blazor UI, MDI canvas, charts, designer workflows, and live WebSocket or SignalR participation as part of the platform design fileciteturn13file0L1-L1 fileciteturn19file0L1-L1
- the broader architecture and controller-centered model require a human interface that can safely express module topology, routeability, and live system state rather than leaving operators to raw terminals alone fileciteturn17file0L1-L1

Session 16 converts those foundations into UI law.

## 3. Session 16 Goals

### Primary Goals
- define the role of the UI as the operator plane of the BCG ecosystem
- define the visual and interaction model for modules, blocks, tensors, routes, sessions, and runtime state
- define safe control workflows for approved operator actions
- define the graph composition and canvas doctrine
- define live session participation and observability experiences
- define role-aware UI boundaries, degraded modes, and audit-friendly actions

### Secondary Goals
- prevent the UI from becoming an unsafe mutation surface
- make runtime state understandable without requiring shell access for everything
- improve operational speed during live updates, incidents, and investigations
- maintain premium presentation without sacrificing production discipline

## 4. Session 16 Deliverables

1. operator-plane constitution  
2. UI role and workflow model  
3. graph canvas doctrine  
4. runtime dashboard and view taxonomy  
5. live session UX model  
6. safe action and confirmation policy  
7. degraded-mode UI behavior rules  
8. accessibility and performance standards  
9. UI observability and audit requirements  
10. UI QA and certification gates  

## 5. Operator-Plane Doctrine

The UI is the governed human-facing expression of the BCG control fabric. It must reflect:
- controller state
- species state
- batch and scheduler state
- tensor lineage summaries
- transport and streaming state
- artifact and rollout state
- trust and session state
- storage and replay readiness

The UI is not a separate truth domain. It is a view and action plane governed by the Block Controller, module species, trust rules, and transport policy.

### UI Doctrine Rules
- the UI must prefer explicit state over inferred state
- the UI must distinguish observation from control
- the UI must make dangerous actions harder than safe actions
- the UI must represent uncertainty, degradation, and failure honestly
- the UI must remain useful during degraded mode, not only during healthy mode

## 6. User and Operator Roles in the UI

## 6.1 Role Classes

### Viewer
May inspect approved summaries, graphs, health views, and session-safe telemetry.

### Analyst
May explore lineage, batch progress, transformation histories, and model or tensor summaries within data visibility policy.

### Operator
May initiate approved operational workflows such as session joining, drain requests, bounded route changes, and observability pivots.

### Maintainer
May perform rollout, cutover, rollback, or configuration actions under policy.

### Security Authority
May inspect trust-sensitive views and execute approved identity, session, or artifact control actions.

### Emergency Authority
May invoke emergency workflows, isolation, or termination under heightened confirmation and audit rules.

## 6.2 Role Rules
- the UI must adapt visible actions to role
- visibility and control depth must be independently governed
- read access does not imply control access
- sensitive or high-risk actions require stronger context and confirmation than informational views

## 7. MDI and Workspace Doctrine

The repo already positions the web app as an MDI-style Blazor surface with specialized modules and canvases fileciteturn13file0L1-L1. Session 16 formalizes how that surface behaves.

## 7.1 Workspace Types

### Observatory Workspace
For health, metrics, routeability, and live runtime summaries.

### Designer Workspace
For graph composition, block topology, tensor ports, and execution design.

### Session Workspace
For live engineering or operational participation, controlled shell output, and rollout progress.

### Incident Workspace
For focused forensic and response views during degradation or failure.

### Species Workspace
For deep inspection of one module species across health, performance, lineage, and runtime behaviors.

## 7.2 Workspace Rules
- workspaces must preserve context across live refreshes
- workspaces must remember bounded user state such as filters and selected entities
- cross-workspace navigation must preserve correlation context where meaningful
- opening many workspaces must not flood the runtime with uncontrolled subscriptions

## 8. Graph Canvas Doctrine

## 8.1 Purpose of the Graph Canvas

The graph canvas is the visual operating surface for block composition, module relations, tensor flow summaries, and execution topology. It is not merely a diagramming tool. It is a governed composition plane.

The graph canvas must support:
- block placement and grouping
- typed sockets and port semantics
- lineage and flow overlays
- execution status indicators
- routeability and health hints
- selection-based detail expansion
- policy-aware editing and review flows

## 8.2 Canvas Rules
- typed compatibility must be visible before connection
- incompatible connections must be prevented, not merely warned after the fact
- disconnected required ports must be visible
- structural changes must be versioned as governed mutations
- live execution overlays must not destroy editing clarity
- high-frequency state must be summarized when necessary to preserve usability

## 8.3 Canvas Modes

### Design Mode
Used for composition, planning, and schema evolution.

### Live Mode
Used for runtime overlays, active block states, stream or batch summaries, and routeability indicators.

### Review Mode
Used for diffing graph revisions, change approval, or forensic comparison.

### Incident Mode
Used for failure hotspot highlighting, blocked edges, stale routes, and degraded execution emphasis.

## 9. View Taxonomy

The UI must provide consistent view families.

## 9.1 Global Control Views
- fabric overview
- controller state
- routeability map
- degraded mode and incident banner
- current rollout state
- active session map

## 9.2 Species Views
- species identity and trust status
- generation and lifecycle phase
- health and routeability
- performance budget status
- transport and storage interactions
- active workloads and backlog summaries

## 9.3 Tensor Views
- lineage summary
- shape and dtype summary
- source and destination species
- transformation chain
- externalization status
- redacted or metadata-only view where sensitivity requires it

## 9.4 Batch and Scheduler Views
- queue depth by lane
- admitted, active, delayed, canceled, failed, completed batches
- fairness and starvation indicators
- overflow and backpressure state

## 9.5 Artifact and Model Views
- artifact identity
- verification and activation state
- rollout lane
- rollback relation
- confidence and drift summaries
- trust state and revocation visibility

## 9.6 Session and Operator Views
- joined sessions
- participant scope
- command outputs
- audit-linked action history
- live update progression
- session health and latency

## 10. Live Session UX Doctrine

Live session participation is a central part of the BCG direction. The UI must support it safely.

## 10.1 Session UX Goals
- let operators join live update sessions without destabilizing runtime
- provide a clean separation between observation and action
- show session scope, permissions, and impact boundaries
- provide durable context for what changed and why

## 10.2 Session UI Requirements
- session banner showing role, scope, and current permissions
- live event feed with filtering and pause capability
- bounded command and action panels
- session timeline view
- rollback readiness or maintenance state indicator
- clear distinction between local UI state and system state

## 10.3 Session Safety Rules
- the UI must never imply that a viewed state is authoritative unless confirmed by controller-backed data
- command execution must surface preview, consequence, and audit implications
- sensitive session actions require stronger confirmation and may require reason entry
- session feed volume must be filterable and rate-governed

## 11. Safe Action Doctrine

Every control action in the UI must be designed for safety.

## 11.1 Action Classes

### Informational Actions
Examples:
- open detail views
- filter dashboards
- inspect lineage
- compare revisions

### Bounded Operational Actions
Examples:
- request drain
- join session
- change filter scope
- open rollout preview
- acknowledge alert

### High-Impact Actions
Examples:
- activate rollout
- perform cutover
- trigger rollback
- revoke routeability
- suspend species
- activate signed artifact

### Emergency Actions
Examples:
- isolate species
- revoke session
- force termination
- enter emergency mode

## 11.2 Confirmation Rules
- informational actions require no heavy confirmation
- bounded operational actions require contextual confirmation where needed
- high-impact actions require explicit confirmation with target, impact, and reason
- emergency actions require elevated role, explicit confirmation, and audit linkage
- “dangerous convenience clicks” are forbidden

## 11.3 Action Design Rules
- actions must surface scope
- actions must surface reversibility or irreversibility
- actions must surface dependencies and blockers
- actions must surface controller acknowledgment state where applicable

## 12. UI Performance Standards

The operator plane must remain responsive even while the platform is busy.

### Performance Rules
- the UI must use summaries, aggregation, and incremental refresh where possible
- uncontrolled high-frequency raw feeds are forbidden in default views
- expensive views must load progressively
- live update surfaces must remain interactive under ordinary runtime pressure
- canvas rendering must degrade gracefully under large graph size
- session feeds must support bounded retention and virtualized rendering where needed

### Runtime Protection Rules
- UI polling must be minimized in favor of governed streaming
- operator views must not create accidental thundering herds against controller or species APIs
- live graph overlays must be rate-limited or summarized if needed
- a failing widget must not collapse the workspace

## 13. Accessibility and Clarity Standards

The UI must remain understandable and operable under pressure.

### Accessibility Rules
- keyboard navigation must be supported for core actions
- color must not be the only carrier of critical state
- status semantics must be readable under reduced motion or simplified display conditions
- text labels for critical controls must remain explicit
- incident and degraded states must be visually prominent but not chaotic

### Clarity Rules
- healthy, degraded, draining, suspended, and failed states must be visually distinct
- confidence and uncertainty must be shown explicitly
- stale data must be labeled as stale
- policy-denied actions must explain why they are unavailable
- loading and waiting states must not masquerade as healthy states

## 14. Trust and Authorization in the UI

The UI must obey the trust fabric rather than flatten it.

### Trust Rules
- the UI must show what the current role may see and do
- denied actions must remain denied even if the button can be rendered for explanation
- trust-sensitive views may require stronger re-authentication or session elevation
- artifact trust, session revocation, and routeability restrictions must be visible where relevant
- the UI must not cache privileged state in a way that outlives permission validity

## 15. Observability and Audit Requirements for the UI

The UI is itself a runtime participant and must be observable.

The system must be able to answer:
- which operator or user opened which live session
- which workspaces were active during critical events
- which actions were requested and whether they were approved or denied
- whether a UI action mapped to a controller-recognized operation
- whether session filters hid important state or whether the operator was shown degraded visibility
- whether the UI entered degraded mode due to runtime protection

### Required UI Signals
- workspace opened
- workspace closed
- session joined
- session left
- live feed paused
- live feed resumed
- action requested
- action confirmed
- action denied
- action completed
- degraded UI mode entered
- degraded UI mode cleared

## 16. Failure and Degraded-Mode Doctrine

The UI must remain safe and useful during failure.

## 16.1 UI Degraded Modes
- read-only mode
- low-detail streaming mode
- cached-summary mode
- incident focus mode
- trust-restricted mode

## 16.2 Degraded-Mode Rules
- degraded mode must be clearly indicated
- degraded mode must preserve critical situational awareness
- unsafe control actions may be disabled in degraded mode
- stale summaries must be labeled
- degraded mode exit must be explicit

## 16.3 UI Failure Classes
- transport disconnect
- stale controller state
- excessive session feed volume
- failed workspace component
- blocked action acknowledgement
- authorization mismatch
- partial canvas rendering failure

None of these failures may silently imply success or health.

## 17. QA and Certification Gates

No operator-plane implementation may claim Session 16 compliance without:
1. declared role and action matrix  
2. workspace taxonomy  
3. graph canvas mode definitions  
4. session UX and safety rules  
5. confirmation and high-impact action policy  
6. degraded-mode behavior notes  
7. accessibility considerations for critical flows  
8. observability and audit coverage for UI actions  
9. performance testing for live views and large graphs  
10. operator runbook and training notes  

## 18. Acceptance Criteria

Session 16 is complete only if:
- the UI is formally defined as an operator plane
- role-aware workspaces and action boundaries are explicit
- graph canvas doctrine is defined
- view taxonomy exists for controller, species, tensors, batches, artifacts, and sessions
- live session UX rules are defined
- safe action and confirmation rules are defined
- degraded-mode behavior is defined
- accessibility and performance standards are declared
- UI observability and QA gates are explicit

## 19. Session 16 Final Statement

Session 16 gives the BCG ecosystem a disciplined human face. The UI becomes the premium operator plane through which runtime, topology, lineage, rollout, and live sessions can be understood and safely influenced. It is not a cosmetic shell around the system. It is a controlled instrument of visibility and action, aligned with controller authority, trust law, runtime continuity, and production safety.
