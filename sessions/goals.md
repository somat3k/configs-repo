# Project Goals

## Long-Term Vision
Build a universal, containerised, network-first mono-repo template that:
- Supports any project design via enumerated/classified module topology.
- Encourages elevating constructions through powerful architectural patterns.
- Enables any module to run standalone AND connect to others through the network.

## Milestone Goals

### M1 — Infra Foundation
- [ ] Redis, PostgreSQL, IPFS running in Docker.
- [ ] Storage Controller with adapters for all three backends.
- [ ] `make infra-up/down` commands working.

### M2 — First Module
- [ ] `auth-module` implemented and standalone.
- [ ] Envelope protocol enforced.
- [ ] Function-as-File rule applied.
- [ ] Health endpoint passing.

### M3 — Service Mesh
- [ ] WebSocket service bus operational.
- [ ] Modules discover and connect automatically.
- [ ] Invoker pattern tested end-to-end.

### M4 — Language Coverage
- [ ] Rust module working.
- [ ] Python module working.
- [ ] Slint UI invoking a module.
- [ ] Solidity contract deployable.

### M5 — Template Polish
- [ ] Sessions template fully documented.
- [ ] All docs rendered correctly in GitHub.
- [ ] CI workflows passing.
- [ ] `.structure_pkg.json` kept in sync.
