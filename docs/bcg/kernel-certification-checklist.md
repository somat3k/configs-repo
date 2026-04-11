# Kernel Certification Checklist
## BCG Session 05 — Production Readiness Gates

> **Status**: ✅ Active  
> **Last Updated**: Session 05

## Certification Gates

- [ ] Unit tests for known input/output behavior
- [ ] Contract tests for input/output arity and type compatibility
- [ ] Lifecycle tests: init, ready, execute, fail, drain, dispose
- [ ] Cancellation and timeout behavior verified
- [ ] Streaming tests for partial and terminal fragments (if applicable)
- [ ] Benchmark coverage for hot execution paths
- [ ] Observability schema verified for lifecycle and failure events
- [ ] Recovery strategy documented (retry/fallback/checkpoint)
- [ ] Operator runbook notes published for this kernel family
