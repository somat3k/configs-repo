# Kernel State Model
## BCG Session 05 — Kernel State Class Constitution

> **Status**: ✅ Active  
> **Last Updated**: Session 05

## 1. Purpose

This document defines state classes used for kernel certification, scheduling, and reset policy.

## 2. State Classes

| Class | Behavior | Required Controls |
|---|---|---|
| `Pure` | Stateless deterministic execution | Contract and latency validation |
| `Stateful` | Bounded mutable state across calls | Reset + snapshot + restore |
| `Streaming` | Progressive output emission | Partial output contract + close reason |
| `Transformational` | Representation/schema/tensor transformations | Lineage + dtype/shape policy |
| `Composite` | Child-kernel orchestration under outer contract | Inner boundary and failure isolation |
| `Training` | Heavy compute and long-running training work | Progress streaming + checkpoint + artifact rules |

## 3. State Rules

- Mutable state MUST be explicit and classed.
- Hidden global mutable state is forbidden.
- Checkpoint-capable kernels MUST define serialization/versioning rules.
- State reset semantics are mandatory for all non-pure kernels.
