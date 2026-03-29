-- Create devdb schema
-- This file runs automatically when the postgres container first starts.

-- Extensions
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Example audit table (used by storage controller)
CREATE TABLE IF NOT EXISTS storage_log (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    backend     TEXT NOT NULL,
    operation   TEXT NOT NULL,
    key         TEXT,
    cid         TEXT,
    session_id  UUID,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
