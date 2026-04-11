# Kernel Execution Context
## BCG Session 05 — Runtime Context Contract

> **Status**: ✅ Active  
> **Last Updated**: Session 05

## 1. Purpose

This document defines the minimum execution context that every kernel invocation must receive.

## 2. Required Context Fields

- `trace_id`
- `correlation_id`
- `cancellation_token`
- `timeout`
- `tenant_id` (optional)
- `resource_budget` (optional)

## 3. Governance Rules

- Cancellation tokens MUST propagate to execution boundaries.
- Timeout budgets MUST be honored when present.
- Trace and correlation identifiers MUST be stable through all emitted partial/final outputs.
- Context values MUST be surfaced in telemetry for post-incident reconstruction.
