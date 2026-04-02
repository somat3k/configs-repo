# Data Layer Module — Session Prompt

## Module Identity
- **Name**: data-layer
- **Namespace**: `MLS.DataLayer`
- **HTTP Port**: 5700
- **WebSocket Port**: 6700

## Data Flow
1. Subscribe to external market data feeds (REST + WebSocket)
2. Normalize data to `MarketDataRecord` type
3. Store raw data in PostgreSQL (partitioned by date)
4. Cache latest prices in Redis (TTL: 5s)
5. Compute features via transformation pipeline
6. Store feature sets in `feature_store` table
7. Broadcast `DATA_UPDATE` envelopes to subscribers

## Key Types
- `MarketDataRecord` — normalized OHLCV with exchange metadata
- `FeatureVector` — computed ML features with schema versioning
- `DataUpdatePayload` — broadcast payload with data and feature updates

## Skills to Apply
- `.skills/storage-data-management.md` — EF Core, Redis, IPFS patterns
- `.skills/websockets-inferences.md` — WebSocket streaming
- `.skills/beast-development.md` — 100k+ ticks/second ingestion
- `.skills/networking.md` — external feed subscriptions
