---
name: storage-data-management
source: github/awesome-copilot/skills/ef-core + postgresql-optimization + custom
description: 'Data storage architecture using PostgreSQL (primary database), Redis (cache layer), and IPFS (distributed storage) — schemas, patterns, and access strategies for the MLS platform.'
---

# Storage & Data Management — MLS Trading Platform

## Storage Architecture
| Storage | Purpose | Technology |
|---------|---------|------------|
| PostgreSQL | Primary relational store — trades, orders, features, addresses | `Npgsql` + `EF Core` |
| Redis | Cache layer — module state, sessions, real-time quotes | `StackExchange.Redis` |
| IPFS | Distributed storage — ML model artifacts, large datasets, snapshots | `Ipfs.Http.Client` |

## PostgreSQL (EF Core) Patterns
- Use EF Core 9 with Npgsql provider
- Separate `IEntityTypeConfiguration<T>` per entity
- Use `AsNoTracking()` for all read-only queries
- Cursor-based pagination (not OFFSET) for large result sets
- Use PostgreSQL JSONB for flexible payload storage
- GIN indexes on all JSONB columns
- Partition large tables (trades, market_data) by date range

## Key Database Schemas
```sql
-- Blockchain addresses (never hardcoded)
CREATE TABLE blockchain_addresses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    label TEXT NOT NULL UNIQUE,  -- enum-like label
    address TEXT NOT NULL,
    chain_id INTEGER NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Feature store
CREATE TABLE feature_store (
    id BIGSERIAL PRIMARY KEY,
    feature_set_name TEXT NOT NULL,
    version INTEGER NOT NULL,
    schema JSONB NOT NULL,
    data_ref TEXT,  -- IPFS CID for large datasets
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Module registry
CREATE TABLE module_registry (
    module_id UUID PRIMARY KEY,
    module_name TEXT NOT NULL,
    endpoint_http TEXT NOT NULL,
    endpoint_ws TEXT NOT NULL,
    status TEXT NOT NULL,
    last_heartbeat TIMESTAMPTZ,
    metadata JSONB
);
```

## Redis Cache Patterns
- Module session state: `mls:module:{module_id}:state`
- Real-time quotes: `mls:quote:{symbol}:latest` (with 5s TTL)
- Inter-module message queues: `mls:queue:{target_module}`
- Distributed locks: `mls:lock:{resource}` (with timeout)
- Use Redis Streams for event log: `mls:events`

## IPFS Storage Patterns
- ML model artifacts: `/ipfs/models/{model_name}/{version}/`
- Large feature matrices: `/ipfs/features/{feature_set}/{timestamp}/`
- Trade snapshots: `/ipfs/snapshots/{date}/`
- Use local IPFS node (Kubo) at `http://localhost:5001`
- Pin all production artifacts to prevent garbage collection
- Store IPFS CIDs in PostgreSQL for reference

## EF Core Best Practices (MLS-specific)
- Use `DbContextFactory` pattern for background workers
- Implement Unit of Work pattern with `IUnitOfWork` interface
- Use compiled queries for hot paths (order lookups, feature retrieval)
- Implement optimistic concurrency for order updates
- Use PostgreSQL advisory locks for distributed critical sections
