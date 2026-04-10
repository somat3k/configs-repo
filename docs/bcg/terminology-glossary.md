# BCG System Terminology Glossary

> **Document Class**: Session 01 Deliverable — Governance Foundation
> **Version**: 1.0.0
> **Status**: Active
> **Session**: 01
> **Last Updated**: 2026-04-10

---

## Purpose

This glossary defines the canonical vocabulary of the BCG program. All sessions, documents, code, comments, and communications must use these terms consistently. Ambiguous or informal language in specifications is not acceptable. When a term does not exist in this glossary and is needed, it must be proposed, defined here, and then used.

---

## A

**Artifact**
A versioned, immutable, identifiable output produced by a training, transformation, or compilation process. Artifacts may be model weights, ONNX files, compiled graphs, or serialized tensor objects. All artifacts must be registered before use.

**Artifact Registry**
The catalog of all known versioned artifacts in the system. Backed by PostgreSQL with IPFS references for large binary payloads. Every artifact has a unique identifier, version, origin module, promotion status, and integrity hash.

---

## B

**Backpressure**
A deliberate flow control mechanism that prevents fast producers from overwhelming slow consumers. Required on all high-frequency internal queues. All `Channel<T>` instances must declare an explicit `BoundedChannelOptions.FullMode` policy.

**Batch Container**
A managed execution unit that groups multiple inference or compute requests into a single scheduled operation. Batch containers declare their parallelism policy, timeout, cancellation behavior, and observability counters.

**BCG (Block Controller Generator)**
The umbrella program name for this ecosystem. BCG denotes the complete production platform including the Block Controller, all specialist modules, the tensor fabric, the documentation system, and the quality assurance framework.

**Block**
The atomic processing unit of the BCG system. A block receives typed input, applies a computation or transformation, and produces typed output. Blocks are organized into composition graphs and governed by the Block Controller.

**Block Controller**
The central orchestration authority of the BCG system. The Block Controller governs module registration, capability routing, health monitoring, heartbeat discipline, transport policy, execution intent, safe degradation, and session-aware live update control. It is not only a router; it is the governance authority.

**Block Kernel**
The execution primitive beneath a block operation. A kernel has a defined lifecycle: init → compute (or stream) → checkpoint → dispose. Kernels may be deterministic or stateful. Stateful kernels carry explicit constraints.

**Block Signal**
The typed data object passed between blocks during graph execution. Block signals carry payload, metadata, trace identifiers, and routing hints.

---

## C

**Capability Registry**
The structured list of capabilities that each registered module declares at startup. The Block Controller uses capability declarations to make routing decisions. Capabilities are typed, versioned, and queryable.

**Charter**
The founding document of a module species. A charter defines what the module is, what it is responsible for, what it is not responsible for, and what its place is in the overall ecosystem.

**Composition Graph**
A nestable container of blocks and connections. A composition graph exposes its disconnected inner sockets as outer ports (fractal nesting). It has a unique graph identifier, a name, and a schema version that must be incremented on every structural change.

**Contract**
A typed, versioned description of what a module or block accepts (input contract) and produces (output contract). Contracts also include tensor contracts (tensor shape, dtype, class) and transport contracts (protocol, serialization, versioning policy).

**Control Tensor**
A tensor used to carry orchestration signals, routing hints, policy states, or governance metadata rather than raw computation data.

---

## D

**DataEvolution Module**
The transformation species of the BCG ecosystem. Its mission is to convert any raw drive, file, stream, or graph payload into the structured, tensor-ready, or graph-ready form expected by BCG modules. It preserves lineage across all transformation steps and never performs destructive hidden mutations.

**Degraded Mode**
A runtime state in which one or more modules are unavailable, unhealthy, or throttled. In degraded mode, the Block Controller applies safe routing policies to avoid propagating failures. Degraded mode behavior must be explicitly documented per module.

**Dtype**
The element data type of a tensor (e.g., float32, float16, int64, bool). Dtype is a required field on every tensor crossing a module boundary.

---

## E

**Envelope**
The standard message wrapper used for all inter-module communication. An envelope carries: `block_id`, `block_sha`, `block_data`, `block_state`, `type`, `version`, `unique_id`, `task_id`, `session_id`, `module_id`, `module_network_address`, `module_network_port`, `timestamp`, and `payload`. No production message may travel unenvelope between modules.

**Execution Intent**
A declaration by an operator or scheduler about what computation a module should perform. Execution intents are routed by the Block Controller and may be queued, scheduled, or rejected based on capability, health, and policy.

---

## F

**Fabric**
The complete communication and orchestration infrastructure of the BCG system. The fabric includes the Block Controller, all transport channels, the capability registry, health monitoring, and the policy engine. The fabric must remain alive during module updates.

**Failure Mode**
A defined, documented way in which a module can fail. Every module must maintain a known failure mode inventory as part of its species assurance documentation.

---

## G

**Governance**
The system of rules, documents, gates, and authorities that control how the BCG ecosystem evolves. Governance is not a post-implementation activity; it is part of implementation.

**Graph Canvas**
The Blazor UI surface on which operators compose block graphs visually. The graph canvas reflects the real composition graph state and dispatches actions through the canonical canvas action dispatcher.

---

## H

**Heartbeat**
A periodic health signal sent by every registered module to the Block Controller. Heartbeat interval is 5 seconds. Missed heartbeats trigger health escalation by the Block Controller.

**Hot Refresh**
A runtime update pattern in which a module or artifact is replaced without stopping the entire fabric. Hot refresh requires safe draining of in-flight requests before the old instance is released.

**Hot-Swap**
A stronger variant of hot refresh in which the new instance begins accepting traffic while the old instance drains. Requires health-aware routing during the transition window.

---

## I

**Inference Tensor**
A tensor produced by or consumed by an ML inference operation. Inference tensors carry dtype, shape, trace ID, model version, and lineage markers.

**IPFS (InterPlanetary File System)**
The distributed storage layer used by BCG for large artifacts, large tensors, and archival objects. IPFS is not used for control-plane state or hot tensor cache. All large tensor payloads above the storage threshold must be routed to IPFS with a content-addressed reference stored in PostgreSQL.

---

## K

**Kernel**
See _Block Kernel_.

---

## L

**Lineage**
The recorded history of transformations applied to a tensor or data object. Every transformation that changes a tensor must append a lineage marker. Lineage enables forensic investigation, reproducibility, and audit.

**Living Document**
A document that is updated as part of implementation rather than as a post-implementation activity. All BCG module documents are living documents. A living document has a version, a last-updated date, and a status.

---

## M

**Module**
An independently deployable, observable, and governed service in the BCG ecosystem. Each module runs in its own Docker container on the `mls-network` bridge network, exposes an HTTP API and a WebSocket server, and registers with the Block Controller on startup.

**Module Species**
The governance model that treats each module as a living entity with its own charter, runtime contract, tensor/transport contract, quality gates, observability, performance budget, recovery notes, and evolution path.

---

## N

**Namespace**
The C# code organization convention used across BCG: `MLS.Core.{Feature}` for shared contracts, `MLS.{ModuleName}.{Feature}` for module code, `MLS.Contracts.{PayloadType}` for typed payload records, and `MLS.{ModuleName}.Tests.{Feature}` for tests.

---

## O

**Observability**
The system capability that allows operators to understand the internal state of the platform through metrics, distributed traces, structured logs, and health signals. Observability is a production requirement, not an optional add-on.

**ONNX (Open Neural Network Exchange)**
The model serialization format used for runtime inference in BCG. The ML Runtime module loads ONNX models. Training pipelines export to ONNX for runtime serving compatibility.

---

## P

**Performance Budget**
The declared set of timing and throughput targets for a module or flow. Every module must declare p50, p95, and p99 targets for its critical operations. Performance budgets are part of the species assurance documentation.

**Policy Engine**
The component inside the Block Controller that evaluates routing decisions against declared policies (capability, health, load, maintenance windows). Policy decisions are deterministic and observable.

**Promotion**
The process of moving an artifact, model, or module version from one environment or status to the next (e.g., from staging to production). Promotions require passing all quality gates and certification checks.

**Protobuf (Protocol Buffers)**
The binary serialization format used for high-trust, high-frequency inter-module communication. Protobuf contracts are versioned and must declare backward-compatibility policy.

---

## Q

**Quality Gate**
A defined checkpoint that a module or artifact must pass before advancing to the next stage. Quality gates include contract verification, health telemetry, rollback procedure, runbook, performance budget confirmation, and failure mode inventory.

---

## R

**Rollback**
A controlled procedure to revert a module, artifact, or configuration change to a known-good previous state. Every module must have a documented rollback procedure. Rollback triggers must be defined.

**Rolling Update**
A deployment pattern in which a new module version is introduced incrementally, health is verified, and the old version is drained and retired. Rolling updates must not collapse the entire fabric.

**Route Governor**
The component inside the Block Controller that makes final routing decisions for all inter-module messages. The route governor consults the capability registry, health monitor, and policy engine before making a decision.

**Runbook**
The operational guide for a module. A runbook describes how to start, stop, monitor, debug, recover, and escalate issues with a specific module. Runbooks are required for production-grade status.

---

## S

**Safe Drain**
The process of allowing a module to finish all in-flight requests before being taken offline. Safe drain is required before any hot refresh or rolling update.

**Schema Version**
A monotonically increasing integer that identifies the structural version of a composition graph or contract. Schema version must be incremented on every structural change.

**Session**
A single focused implementation context window with a defined goal, objective targets, deliverables, and exit criteria. Sessions are the atomic unit of BCG program progress.

**Shell VM**
The module that provides a privileged runtime interface for operator interventions. Shell commands have ownership semantics, dangerous commands require elevated authorization, and destructive actions are logged.

**SignalR**
The ASP.NET Core real-time framework used for WebSocket-based eventing, session control, and streaming between BCG modules and the web application.

**SLO (Service Level Objective)**
A quantified target for a specific aspect of a module's behavior (e.g., p95 routing decision < 10 ms). SLOs are derived from performance budgets and monitored via observability dashboards.

**Socket**
A typed connection point on a block. Sockets have a direction (input or output), a data type from `BlockSocketType`, and a connection state. Type mismatches must throw `InvalidBlockConnectionException`.

**Species**
See _Module Species_.

---

## T

**Tensor**
A multi-dimensional array of typed numerical values (or references to externalized values) that serves as the primary advanced execution structure in BCG. Tensors carry identity, dtype, shape, lineage, trace ID, and persistence handles.

**Tensor Contract**
The formal declaration of the tensor types, shapes, and dtypes that a module or block accepts and produces. Tensor contracts are versioned and part of each module's 20-document species pack.

**TensorTrainer Module**
The heavy-compute training species of the BCG ecosystem. Its mission is to provide tensor-native training capacity for scientific, mathematical, code, and text generation workloads. Status: Reserved — Session 08.

**Tensorification**
The process of introducing tensor-native execution paths into BCG modules, flows, and contracts. Tensorification is the core systems direction declared in Session 01.

**Topic**
A named channel used for pub/sub messaging through the Block Controller hub. Modules subscribe to topics to receive relevant envelopes. Topics are typed strings declared as constants in `MLS.Core.Constants`.

**Transport Contract**
The formal declaration of the communication protocol, serialization format, versioning policy, retry behavior, timeout rules, observability metadata, and fallback behavior for a module's external interfaces.

**Trace ID**
A globally unique identifier that is propagated across all operations in a single logical flow. Trace IDs enable distributed tracing and forensic investigation.

---

## U

**Universal Tensor**
The canonical tensor structure defined in Session 03 that all BCG modules must be capable of accepting or producing on advanced execution lanes. A universal tensor carries: unique ID, dtype, shape, payload or reference, origin block/module, timestamp, trace ID, tags, lineage markers, and optional persistence handles.

---

## V

**Version**
A monotonically increasing integer or semantic version string attached to a contract, artifact, envelope, or document. No production path may use an unversioned contract. Version 0 is prohibited; all versions start at 1.

---

## W

**WebSocket**
The bidirectional persistent connection protocol used for live eventing, streaming, and real-time session control in BCG. Every module exposes a WebSocket server (SignalR hub) in addition to its HTTP API.

---

## Z

**Zero-Allocation Path**
A performance-critical code path engineered to produce no heap allocations in steady state. The envelope routing hot path must be zero-allocation, using `ArrayPool<byte>`, `Span<byte>`, and pre-allocated `OrtValue` objects.
