# shell-vm — Session 12: Database Schema

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 12. Database Schema


```sql
-- Enable pgcrypto for gen_random_uuid() (idempotent, safe to re-run)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Persistent execution block registry
CREATE TABLE execution_blocks (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    label               TEXT NOT NULL,
    state               TEXT NOT NULL,
    shell               TEXT NOT NULL DEFAULT '/bin/sh',
    working_directory   TEXT NOT NULL DEFAULT '/app',
    environment         JSONB,
    requesting_module_id TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    exit_code           INT
);

-- Full audit log — never truncated; partition by month.
-- A DEFAULT partition is required so inserts succeed before month-specific
-- partitions are created by the monthly maintenance job.
CREATE TABLE shell_audit_log (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    block_id    UUID REFERENCES execution_blocks(id),
    command     TEXT NOT NULL,
    started_at  TIMESTAMPTZ NOT NULL,
    ended_at    TIMESTAMPTZ,
    exit_code   INT,
    duration_ms BIGINT,
    module_id   TEXT
) PARTITION BY RANGE (started_at);

-- Default partition catches rows that don't match any month partition yet.
-- This partition can remain indefinitely as a safety catch-all, or data can be
-- migrated to month-specific partitions before it is dropped (PostgreSQL requires
-- the partition to be empty before it can be removed).
CREATE TABLE shell_audit_log_default PARTITION OF shell_audit_log DEFAULT;
```

---
