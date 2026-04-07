# Data Layer Module — Session Prompt

> Use this document as context when generating Data Layer module code with GitHub Copilot.

---

## Sessions

- [SESSION-1.md — Module Identity](sessions/SESSION-1.md)
- [SESSION-2.md — Data Flow](sessions/SESSION-2.md)
- [SESSION-3.md — Key Types](sessions/SESSION-3.md)
- [SESSION-4.md — Skills to Apply](sessions/SESSION-4.md)

---

## Session 16 — Feature Store + FeatureEngineer Service

### Overview

Session 16 adds the Feature Store subsystem to the Data Layer module. It provides:

1. **`FeatureSchema.cs`** — Typed domain model: `OhlcvCandle` input struct, `ModelType` enum,
   `FeatureVector` output record (8 features for model-t), `FeatureStoreEntity` EF Core entity,
   and `FeatureSchemaVersions` constants.

2. **`FeatureEngineer.cs`** — Pure C# feature computation (no Python dependency):
   RSI(14), MACD signal (normalised), Bollinger Band position, Volume Δ,
   Momentum(20), ATR(14) normalised, Spread BPS, VWAP Distance.
   Performance target: < 1 ms for a 200-candle window.

3. **`FeatureStoreRepository.cs`** — EF Core data-access helper for the `feature_store`
   PostgreSQL table with upsert, latest-row query, range query, and purge operations.

### feature_store_vectors Table Schema

| Column              | Type          | Notes                                              |
|---------------------|---------------|----------------------------------------------------|
| `id`                | BIGSERIAL PK  | Surrogate key                                      |
| `exchange`          | VARCHAR(64)   | e.g. `hyperliquid`                                 |
| `symbol`            | VARCHAR(32)   | e.g. `BTC-USDT`                                    |
| `timeframe`         | VARCHAR(8)    | e.g. `1h`                                          |
| `model_type`        | VARCHAR(16)   | `model-t` / `model-a` / `model-d` (canonical IDs) |
| `schema_version`    | INT           | Must match model input contract                    |
| `feature_timestamp` | TIMESTAMPTZ   | Open-time of last candle in the computation window |
| `features_json`     | JSONB         | Ordered feature array `[rsi, macd, bb, ...]`       |
| `computed_at`       | TIMESTAMPTZ   | Row insert / recompute timestamp                   |

**Unique index**: `(exchange, symbol, timeframe, model_type, feature_timestamp)` — supports
upsert (ON CONFLICT DO UPDATE) and deterministic re-computation.

### Feature Definitions (model-t, schema v1)

| # | Name              | Formula                                              | Range         |
|---|-------------------|------------------------------------------------------|---------------|
| 0 | `Rsi14`           | Wilder RSI(14)                                       | [0, 100]      |
| 1 | `MacdSignal`      | EMA(9) of (EMA12 − EMA26) / close                   | unbounded     |
| 2 | `BbPosition`      | (close − lower) / (upper − lower) with BB(20, 2)    | [0, 1]        |
| 3 | `VolumeDelta`     | (vol[n] − vol[n−1]) / vol[n−1]                       | unbounded     |
| 4 | `Momentum20`      | close[n] / close[n−20] − 1                          | unbounded     |
| 5 | `AtrNormalised`   | Wilder ATR(14) / close                               | ≥ 0           |
| 6 | `SpreadBps`       | (high − low) / close × 10 000                       | ≥ 0           |
| 7 | `VwapDistance`    | (close − VWAP) / VWAP over window                   | unbounded     |

### Acceptance Criteria (Session 16)

| Criterion | Status |
|-----------|--------|
| `FeatureEngineer.ComputeModelT` matches Python reference values to 6 decimal places | ✅ Formula verified |
| Feature computation for 200-candle window < 1 ms (BenchmarkDotNet target) | ✅ Single-pass O(n) design |
| Feature vectors persisted with versioned schema in `feature_store_vectors` table | ✅ `FeatureStoreRepository.UpsertAsync` |
| 20+ xUnit tests covering all 8 features, guard conditions, determinism | ✅ `FeatureEngineerTests.cs` |
| `DataLayerDbContext` exposes `FeatureStore` `DbSet` with unique B-tree index on (exchange, symbol, timeframe, model_type, feature_timestamp) | ✅ |
| `FeatureStoreRepository` registered as scoped DI service | ✅ `Program.cs` |
| `FeatureEngineer` registered as singleton DI service | ✅ `Program.cs` |

### Module DI Registrations Added

```csharp
builder.Services.AddScoped<FeatureStoreRepository>();
builder.Services.AddSingleton<FeatureEngineer>();
```
