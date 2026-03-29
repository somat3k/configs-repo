# Storage Controller

## Overview
The Storage Controller is the single authoritative layer for all read/write operations. No module accesses databases directly — they call the controller via the internal service bus.

## Storage Backends

| Backend    | Use Case                                    | Access Pattern          |
|------------|---------------------------------------------|-------------------------|
| Redis      | Session cache, pub/sub, rate limiting       | Key-Value, Streams      |
| PostgreSQL | Relational data, user records, event log    | SQL queries             |
| IPFS       | Content-addressed blobs, large files, media | CID get/put             |

## Directory Layout

```
src/
  storage/
    controller.rs / controller.py  ← single entry point
    backends/
      redis.rs / redis.py          ← Redis adapter
      postgres.rs / postgres.py    ← Postgres adapter
      ipfs.rs / ipfs.py            ← IPFS HTTP client
    models/                        ← shared data models
    migrations/                    ← SQL migration files
```

## Controller API (canonical commands)

### Commands (write)

Use the `StorageOperation` enum values from `.structure_pkg.json`:

| Enum Path                        | Backend    | Description                    |
|----------------------------------|------------|--------------------------------|
| `StorageOperation::Put`          | IPFS       | Store a blob, return CID       |
| `StorageOperation::Set`          | Redis      | Set a key-value pair           |
| `StorageOperation::Insert`       | Postgres   | Insert a record                |
| `StorageOperation::Update`       | Postgres   | Update a record                |
| `StorageOperation::Delete`       | Postgres   | Soft-delete a record           |
| `StorageOperation::CacheSet`     | Redis      | Set with TTL                   |
| `StorageOperation::Publish`      | Redis      | Publish to a channel           |

### Queries (read)

| Enum Path                        | Backend    | Description                    |
|----------------------------------|------------|--------------------------------|
| `StorageOperation::Get`          | IPFS       | Retrieve content by CID        |
| `StorageOperation::Fetch`        | Redis      | Get a key value                |
| `StorageOperation::Select`       | Postgres   | Run a parameterised query      |
| `StorageOperation::CacheGet`     | Redis      | Get cached value               |
| `StorageOperation::Subscribe`    | Redis      | Subscribe to a channel         |

## Data Flow Diagram

```mermaid
flowchart TD
    Module["Any Module"]
    SC["Storage Controller"]
    RC["Redis Client"]
    PC["Postgres Client"]
    IC["IPFS Client"]

    Module -->|"Envelope(type=Command)"| SC
    SC --> RC & PC & IC
    RC -->|"Redis\n:6379"| RedisDB[("Redis")]
    PC -->|"Postgres\n:5432"| PgDB[("PostgreSQL")]
    IC -->|"IPFS API\n:5001"| IpfsNode[("IPFS Node")]
```

## Redis Key Schema

```
<module>:<entity>:<id>            # entity cache
session:<session_id>:state        # session state
ratelimit:<user_id>:<action>      # rate limiter
pubsub:<channel>                  # pub/sub channel
lock:<resource>                   # distributed lock
```

## Postgres Conventions

- All tables have `id UUID PRIMARY KEY DEFAULT gen_random_uuid()`.
- All tables have `created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()`.
- Soft-delete via `deleted_at TIMESTAMPTZ`.
- Migrations in `src/storage/migrations/` named `NNNN_description.sql`.

## IPFS Conventions

- Store only content-addressed data (blobs, files, media).
- Always pin CIDs that must survive garbage collection.
- Reference CIDs in Postgres via a `cid TEXT` column on relevant tables.
