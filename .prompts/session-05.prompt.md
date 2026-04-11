---
mode: agent
description: "BCG Session 05 — Runtime Kernel Model and Block Execution Fabric"
status: "⏳ Pending — documentation and C# kernel infrastructure required"
depends-on: ["session-02", "session-03", "session-04"]
produces: ["docs/bcg/session-05-*.md", "src/core/MLS.Core/Kernels/", "src/block-controller/"]
---

# Session 05 — Runtime Kernel Model and Block Execution Fabric

> **Status**: ⏳ Pending — no kernel infrastructure exists yet.

## Session Goal

Define the kernel as the universal execution primitive: every executable block resolves to a governed kernel with a declared lifecycle, state class, tensor profile, and observability contract.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-05-extended-document.md` (source: `.prompts-update/BCG_Session_05_Extended_Document.md`)
- [ ] `kernel-lifecycle-spec.md` — Init → Ready → Running → Streaming → Checkpointing → Disposing phases
- [ ] `kernel-state-model.md` — Pure / Stateful / Streaming / Transformational / Composite / Training classes
- [ ] `block-to-kernel-resolution.md` — 8-step resolution pipeline, block identity → kernel placement
- [ ] `kernel-execution-context.md` — CancellationToken, timeout, tenant, trace, resource budget
- [ ] `kernel-certification-checklist.md` — QA gates before a kernel is production-certified

### C# Kernel Abstractions (`src/core/MLS.Core/Kernels/`)
- [ ] `IKernel.cs` — `InitAsync`, `ExecuteAsync`, `DisposeAsync`; declare `KernelDescriptor`
- [ ] `IStreamingKernel.cs` — extends `IKernel` with `IAsyncEnumerable<KernelOutput> StreamAsync(...)`
- [ ] `IStatefulKernel.cs` — extends `IKernel` with `SnapshotAsync`, `RestoreAsync`, `ResetAsync`
- [ ] `KernelDescriptor.cs` — record: operationId, inputContract, outputContract, stateClass, executionModes, performanceBudget
- [ ] `KernelState.cs` — enum: Uninitialized, Initializing, Ready, Running, Streaming, Checkpointing, Disposing, Faulted
- [ ] `KernelExecutionContext.cs` — record: traceId, correlationId, cancellationToken, timeout, tenantId, resourceBudget
- [ ] `KernelOutput.cs` — record: data (BcgTensor), isFinal, fragmentIndex, traceId
- [ ] `KernelStateClass.cs` — enum: Pure, Stateful, Streaming, Transformational, Composite, Training
- [ ] `KernelRegistry.cs` — `ConcurrentDictionary<string, IKernelFactory>`, `Register`, `Resolve`
- [ ] `IKernelFactory.cs` — `CreateKernel(KernelDescriptor, IServiceProvider): IKernel`

### Block-to-Kernel Resolution (`src/block-controller/`)
- [ ] Add `KernelResolutionService.cs` — resolves a block request to a kernel via `KernelRegistry`
- [ ] Add `KernelScheduler.cs` — policy-aware placement: capability match, health score, load factor
- [ ] Emit `KERNEL_INITIALIZED`, `KERNEL_FAULTED`, `KERNEL_DISPOSED` envelope events

### Tests (`src/core/MLS.Core.Tests/Kernels/`)
- [ ] `KernelRegistryTests.cs` — register, resolve, missing key throws
- [ ] `KernelDescriptorTests.cs` — immutability and equality
- [ ] `KernelExecutionContextTests.cs` — cancellation propagation
- [ ] Integration: a `PureKernel` reference implementation executes end-to-end via `KernelResolutionService`

## Skills to Apply

```
.skills/system-architect.md          — execution fabric, kernel lifecycle governance
.skills/dotnet-devs.md               — IAsyncEnumerable<T>, Channel<T>, records, CancellationToken
.skills/beast-development.md         — ArrayPool, BoundedChannel, SemaphoreSlim throttle
.skills/machine-learning.md          — tensor input/output contracts, ONNX alignment
.skills/websockets-inferences.md     — streaming kernel output over SignalR
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — kernel events via typed EnvelopePayload
- No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in kernel async code
- Use `ConfigureAwait(false)` in all kernel infrastructure code
- `Channel<T>` with `BoundedChannelOptions` and explicit `FullMode` for all kernel output queues

## Acceptance Gates

- [ ] `IKernel` interface compiles; `KernelRegistry` registers and resolves a test kernel
- [ ] Streaming kernel yields `IAsyncEnumerable<KernelOutput>` without blocking the caller
- [ ] `KernelExecutionContext.CancellationToken` propagates into `ExecuteAsync`
- [ ] All new tests pass: `dotnet test src/core/MLS.Core.Tests/`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Kernels/` | Create all kernel abstractions here |
| `src/core/MLS.Core/Tensor/BcgTensor.cs` | Tensor types used by KernelOutput |
| `src/block-controller/MLS.BlockController/Services/` | Add KernelResolutionService here |
| `src/core/MLS.Core/Constants/MessageTypes.cs` | Add KERNEL_* event constants |
| `.prompts-update/BCG_Session_05_Extended_Document.md` | Full session spec |
