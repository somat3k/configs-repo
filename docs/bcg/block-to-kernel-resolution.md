# Block-to-Kernel Resolution
## BCG Session 05 — Resolution Pipeline and Placement Rules

> **Status**: ✅ Active  
> **Last Updated**: Session 05

## 1. Purpose

This document formalizes the execution resolution path from operator-facing block intent to production kernel execution.

## 2. Resolution Pipeline

1. Block identity lookup
2. Operation type resolution
3. Capability verification
4. Kernel registry lookup
5. Configuration binding
6. Policy validation
7. Runtime placement decision
8. Kernel initialization or reuse
9. Lane assignment and execution admission

## 3. Resolution Guarantees

- No block executes without a resolved kernel identity.
- No kernel executes without capability + policy validation.
- No implicit kernel substitution is allowed unless policy explicitly permits fallback.
- Placement decisions must be observable and reproducible from emitted telemetry.
