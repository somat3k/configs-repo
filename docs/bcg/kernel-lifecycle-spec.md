# Kernel Lifecycle Specification
## BCG Session 05 — Runtime Kernel Phase Constitution

> **Status**: ✅ Active  
> **Last Updated**: Session 05  
> **Owned by**: Block Controller + Kernel-bearing modules

## 1. Purpose

This document defines the governed lifecycle phases for production kernels and the legal transition rules required by the execution fabric.

## 2. Lifecycle Phases

| Phase | Meaning | Accepts Work |
|---|---|---|
| `Uninitialized` | Kernel descriptor is known but resources are not prepared | ❌ |
| `Initializing` | Dependencies, model weights, and resources are being prepared | ❌ |
| `Ready` | Kernel is healthy and schedulable | ✅ |
| `Running` | Kernel is executing a non-stream invocation | ⚠️ In-flight only |
| `Streaming` | Kernel is executing and emitting partial outputs | ⚠️ In-flight only |
| `Checkpointing` | Kernel is creating/restoring checkpoint state | ❌ |
| `Disposing` | Kernel is draining and releasing resources | ❌ |
| `Faulted` | Kernel entered an invalid/failed state | ❌ |

## 3. Transition Rules

- All lifecycle transitions MUST be emitted as structured telemetry.
- Invalid transitions MUST be treated as runtime errors.
- Entering `Faulted` MUST include a reason code.
- Draining/disposal transitions MUST complete without admitting new work.
- Stateful kernels MUST expose reset/checkpoint boundaries before reuse.

## 4. Required Events

Block Controller emits:
- `KERNEL_INITIALIZED`
- `KERNEL_FAULTED`
- `KERNEL_DISPOSED`

Each event MUST include block ID, kernel operation ID, trace ID, module ID, lane ID, state, timestamp, and optional reason.
