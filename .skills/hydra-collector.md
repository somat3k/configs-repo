---
name: hydra-collector
source: custom (MLS Trading Platform)
description: 'Hydra data collection patterns — feed collector lifecycle, gap detection algorithm, backfill pipeline, feature store schema, and real-time candle routing to designer blocks.'
---

# Hydra Data Collector — MLS Trading Platform

> Apply this skill when working on: `MLS.DataLayer.Hydra`, feed collectors, gap detection, backfill pipeline, feature engineering, or `DataHydra` blocks in the designer.

---

## Feed Collector Rules

```csharp
// 1. Extend FeedCollector base class — don't implement BackgroundService directly
// 2. Use bounded Channel<OHLCVCandle>(4096) with DropOldest for back-pressure
// 3. Implement exponential backoff: base 1s, max 60s, jitter (prevent thundering herd)
// 4. ALWAYS upsert (INSERT ... ON CONFLICT DO NOTHING) — never assume no duplicates
// 5. Update Redis cache AFTER PostgreSQL write (eventual consistency, cache is L1 read path)
// 6. Emit CANDLE_STREAM envelope for each candle to Block Controller
```

## Gap Detection Rules

```csharp
// Run on 1-minute timer via BackgroundService
// Tolerance: 5% (if actual < expected * 0.95, gap detected)
// Use Parallel.ForEachAsync over all active (exchange, symbol, timeframe) combinations
// On gap detection:
//   1. Emit DATA_GAP_DETECTED envelope to Block Controller
//   2. Enqueue to BackfillPipeline (Channel<DataGap>)
// On backfill complete:
//   1. Emit DATA_GAP_FILLED envelope
```

## Backfill Pipeline Rules

```csharp
// Max concurrent backfill jobs: 4 (configurable)
// Rate limit: 5 REST API calls/second per exchange (configurable)
// Chunk size: 1000 candles per REST request (exchange-dependent)
// Retry: 3 attempts with exponential backoff
// After 3 failures: emit DATA_GAP_FAILED envelope with reason
```

## Feature Engineering Rules

```csharp
// FeatureEngineer MUST use System.Numerics.Vector<float> for vectorised computation
// Target: < 1ms for 200-candle window (L1 acceleration)
// Feature values MUST match Python reference implementation to 6 decimal places
// Store computed features in feature_store table with schema_version
// Schema version MUST match model's expected input dimension
// NEVER use Python for production inference feature computation (only C# ONNX path)
```

## FeatureStore Query Pattern

```csharp
// Load features for training (called by DataLoaderBlock)
// Use compiled EF Core query for performance
// Arrow feather format for large batch export to IPFS/training
var features = await _db.FeatureStore
    .Where(f => f.Exchange == exchange && f.Symbol == symbol
                && f.ModelType == modelType && f.SchemaVersion == schemaVersion
                && f.OpenTime >= from && f.OpenTime < to)
    .OrderBy(f => f.OpenTime)
    .AsNoTracking()
    .ToArrayAsync(ct);
```

## Data Flow to Designer Canvas

```
FeedCollector receives candle
  → Persist to PostgreSQL
  → Update Redis latest_candle:{exchange}:{symbol}:{timeframe}
  → Emit CANDLE_STREAM envelope
    → Block Controller routes to subscribed FeedSourceBlock
      → FeedSourceBlock.ProcessAsync(candle)
        → Output on CandleStream socket
          → Connected IndicatorBlocks process
            → Chart panel updates via JS interop
```
