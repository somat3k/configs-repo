-- MLS Trading Platform — PostgreSQL Initialization
-- Run automatically by docker-entrypoint-initdb.d

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS "btree_gin";

-- ─────────────────────────────────────────────
-- Module Registry
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS module_registry (
    module_id       UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    module_name     TEXT NOT NULL UNIQUE,
    endpoint_http   TEXT NOT NULL,
    endpoint_ws     TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'offline',
    last_heartbeat  TIMESTAMPTZ,
    metadata        JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_module_registry_status ON module_registry(status);
CREATE INDEX idx_module_registry_metadata ON module_registry USING gin(metadata);

-- ─────────────────────────────────────────────
-- Blockchain Addresses (never hardcoded)
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS blockchain_addresses (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    label       TEXT NOT NULL UNIQUE,
    address     TEXT NOT NULL,
    chain_id    INTEGER NOT NULL,
    chain_name  TEXT NOT NULL,
    is_active   BOOLEAN NOT NULL DEFAULT true,
    metadata    JSONB,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_blockchain_addresses_label ON blockchain_addresses(label);
CREATE INDEX idx_blockchain_addresses_chain ON blockchain_addresses(chain_id);

-- ─────────────────────────────────────────────
-- Feature Store (ML features)
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS feature_store (
    id              BIGSERIAL PRIMARY KEY,
    feature_set_name TEXT NOT NULL,
    version         INTEGER NOT NULL DEFAULT 1,
    schema          JSONB NOT NULL,
    data_ref        TEXT,       -- IPFS CID for large datasets
    row_count       BIGINT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(feature_set_name, version)
);

CREATE INDEX idx_feature_store_name ON feature_store(feature_set_name);
CREATE INDEX idx_feature_store_schema ON feature_store USING gin(schema);

-- ─────────────────────────────────────────────
-- Model Registry (ML models)
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS model_registry (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    model_name      TEXT NOT NULL,
    version         TEXT NOT NULL,
    model_type      TEXT NOT NULL,
    onnx_ref        TEXT,       -- IPFS CID or local path
    joblib_ref      TEXT,       -- IPFS CID or local path
    metrics         JSONB,
    is_active       BOOLEAN NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(model_name, version)
);

CREATE INDEX idx_model_registry_name ON model_registry(model_name);
CREATE INDEX idx_model_registry_active ON model_registry(is_active) WHERE is_active = true;

-- ─────────────────────────────────────────────
-- Module Event Log
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS module_events (
    id          BIGSERIAL PRIMARY KEY,
    module_id   UUID NOT NULL,
    event_type  TEXT NOT NULL,
    session_id  UUID,
    payload     JSONB NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
) PARTITION BY RANGE (created_at);

CREATE INDEX idx_module_events_module ON module_events(module_id);
CREATE INDEX idx_module_events_type ON module_events(event_type);
CREATE INDEX idx_module_events_payload ON module_events USING gin(payload);

-- Create initial monthly partition
CREATE TABLE module_events_default PARTITION OF module_events DEFAULT;

-- ─────────────────────────────────────────────
-- Market Data (partitioned by date)
-- ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS market_data (
    id          BIGSERIAL,
    symbol      TEXT NOT NULL,
    exchange    TEXT NOT NULL,
    timestamp   TIMESTAMPTZ NOT NULL,
    open        NUMERIC(20, 8) NOT NULL,
    high        NUMERIC(20, 8) NOT NULL,
    low         NUMERIC(20, 8) NOT NULL,
    close       NUMERIC(20, 8) NOT NULL,
    volume      NUMERIC(30, 8) NOT NULL,
    timeframe   TEXT NOT NULL DEFAULT '1m'
) PARTITION BY RANGE (timestamp);

CREATE INDEX idx_market_data_symbol_time ON market_data(symbol, timestamp DESC);

-- Create initial partition
CREATE TABLE market_data_default PARTITION OF market_data DEFAULT;

DO $$
BEGIN
  EXECUTE format(
    'COMMENT ON DATABASE %I IS ''Machine Learning Studio for Trading, Arbitrage, and DeFi''',
    current_database()
  );
END;
$$;
