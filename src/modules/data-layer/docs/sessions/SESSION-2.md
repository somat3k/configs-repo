# data-layer — Session 2: Data Flow

> Use this document as context when generating Data Layer module code with GitHub Copilot.

## Data Flow

1. Subscribe to external market data feeds (REST + WebSocket)
2. Normalize data to `MarketDataRecord` type
3. Store raw data in PostgreSQL (partitioned by date)
4. Cache latest prices in Redis (TTL: 5s)
5. Compute features via transformation pipeline
6. Store feature sets in `feature_store` table
7. Broadcast `DATA_UPDATE` envelopes to subscribers
