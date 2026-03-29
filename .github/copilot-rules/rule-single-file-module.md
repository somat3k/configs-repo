# Rule: Single-File Module

## Purpose
Every service module must be buildable and runnable as a single standalone binary/process, independently of other modules.

## Enforcement

### Required per module
- Own entry point (`main.rs`, `__main__.py`, or equivalent).
- Own `Dockerfile` at `src/modules/<name>/Dockerfile`.
- Entry in `infra/docker-compose.yml`.
- `GET /health` endpoint returning `{"status":"ok","module":"<name>"}`.
- `GET /info` endpoint returning module metadata.
- `POST /invoke` endpoint accepting `Envelope`.

### What to flag
- Modules that import internal functions from other modules directly.
- Modules without a health endpoint.
- Modules not present in `infra/docker-compose.yml`.
- Modules not registered in `.structure_pkg.json → modules[]`.

### Build targets
Each module must be usable with:
```bash
make run MODULE=<name>          # run standalone
make build-module MODULE=<name> # build the binary
make test-module MODULE=<name>  # run unit tests
```

## Example module structure

```
src/modules/auth/
  main.rs           ← entry point
  commands/
    login.rs
    logout.rs
  queries/
    get_session.rs
  Dockerfile
```
