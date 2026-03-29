# Session Configs

This file documents the default configuration values used across sessions. Override in each session's `SESSION.md` or local `.env`.

---

## Infrastructure

```env
# Redis
REDIS_URL=redis://localhost:6379
REDIS_PASSWORD=

# PostgreSQL
DATABASE_URL=postgresql://postgres:postgres@localhost:5432/devdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=devdb

# IPFS
IPFS_API=http://localhost:5001
IPFS_GATEWAY=http://localhost:8080
```

## Module Ports

| Module          | Default Port |
|-----------------|--------------|
| auth-module     | 8001         |
| storage-module  | 8002         |
| compute-module  | 8003         |
| service-bus     | 9000         |

## Service Bus

```env
BUS_URL=ws://localhost:9000/bus
BUS_HEARTBEAT_INTERVAL=30
```

## Logging

```env
LOG_LEVEL=info
LOG_FORMAT=json
```

## Build

```env
RUST_EDITION=2021
PYTHON_VERSION=3.12
SOLIDITY_VERSION=0.8.20
```
