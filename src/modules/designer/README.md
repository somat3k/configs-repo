# MLS Designer Module

> **Module identity**: `designer` · HTTP `5250` · WebSocket `6250`

The Designer module is the block-graph composition engine for the MLS trading platform.
It provides a catalog of reusable, typesafe processing blocks, connects them into executable
strategy graphs, and dispatches deployment envelopes to the Block Controller.

---

## Architecture

```
┌──────────────────────── Designer Module ─────────────────────────────┐
│                                                                       │
│  BlockRegistry (IBlockRegistry)                                       │
│    └── 28 block types: DataSource · Indicator · ML · Strategy        │
│                        Risk · Execution                               │
│                                                                       │
│  DesignerHub  (SignalR /hubs/designer)                                │
│  BlocksController (REST /api/blocks)                                  │
│  BlockControllerClient (MODULE_REGISTER + MODULE_HEARTBEAT)          │
│                                                                       │
└──────────────────────────────────────────────────────────────────────┘
          │ MODULE_REGISTER / HEARTBEAT
          ▼
   Block Controller (5100/6100)
```

---

## Ports

| Protocol | Port | Endpoint |
|----------|------|----------|
| HTTP API | 5250 | `http://designer:5250` |
| WebSocket / SignalR | 6250 | `ws://designer:6250/hubs/designer` |

---

## Block Catalog

All blocks implement `IBlockElement` and are registered in `BlockRegistry` on startup.

### DataSource Blocks (4)

| Block | Input | Output | Description |
|-------|-------|--------|-------------|
| `CandleFeedBlock` | — | `CandleStream` | Live OHLCV candle feed |
| `OrderBookFeedBlock` | — | `OrderBookUpdate` | Level-2 depth updates |
| `TradeFeedBlock` | — | `TradeTickStream` | Tick-by-tick trades |
| `BacktestReplayBlock` | — | `CandleStream` | Historical replay from PostgreSQL |

### Indicator Blocks (7)

| Block | Input | Output | Algorithm |
|-------|-------|--------|-----------|
| `RSIBlock` | `CandleStream` | `IndicatorValue` [0,1] | Wilder's 14-period RSI |
| `MACDBlock` | `CandleStream` | `IndicatorValue` | MACD histogram |
| `BollingerBlock` | `CandleStream` | `IndicatorValue` [0,1] | Band position |
| `ATRBlock` | `CandleStream` | `IndicatorValue` | Average True Range |
| `VWAPBlock` | `CandleStream` | `IndicatorValue` | VWAP deviation |
| `VolumeProfileBlock` | `CandleStream` | `IndicatorValue` [0,1] | Volume percentile |
| `CustomIndicatorBlock` | `CandleStream` / `IndicatorValue` | `IndicatorValue` | Passthrough (Roslyn in Phase 6) |

### ML Blocks (4)

| Block | Input | Output | Model |
|-------|-------|--------|-------|
| `ModelTInferenceBlock` | `FeatureVector` | `MLSignal` | model-t (trader) |
| `ModelAInferenceBlock` | `FeatureVector` | `ArbitrageOpportunity` | model-a (arbitrager) |
| `ModelDInferenceBlock` | `FeatureVector` | `DeFiSignal` | model-d (defi) |
| `EnsembleBlock` | `MLSignal` × 3 | `MLSignal` | Weighted-vote consensus |

### Strategy Blocks (4)

| Block | Inputs | Output | Strategy |
|-------|--------|--------|---------|
| `MomentumStrategyBlock` | `CandleStream`, `IndicatorValue` | `MLSignal` | Momentum + volume filter |
| `MeanReversionBlock` | `IndicatorValue` | `MLSignal` | Z-score mean reversion |
| `TrendFollowBlock` | `CandleStream` | `MLSignal` | EMA crossover |
| `CompositeStrategyBlock` | exposed inner ports | exposed inner ports | Fractal composition |

### Risk Blocks (4)

| Block | Input | Output | Rule |
|-------|-------|--------|------|
| `PositionSizerBlock` | `MLSignal` | `RiskDecision` | Fixed fraction / Kelly |
| `StopLossBlock` | `CandleStream`, `IndicatorValue` | `RiskDecision` | Trailing / ATR stop |
| `DrawdownGuardBlock` | `MLSignal` | `RiskDecision` | Max drawdown halt |
| `ExposureLimitBlock` | `MLSignal` | `RiskDecision` | Portfolio exposure cap |

### Execution Blocks (4)

| Block | Input | Output | Role |
|-------|-------|--------|------|
| `OrderEmitterBlock` | `RiskDecision` | `TradeOrder` | Emit order to broker |
| `OrderRouterBlock` | `TradeOrder` | `TradeOrder` | Smart route: HYPERLIQUID / Camelot |
| `FillTrackerBlock` | `OrderResult` | `OrderResult` / `TradeOrder` | Await fill, retry |
| `SlippageEstimatorBlock` | `TradeOrder`, `OrderBookUpdate` | `IndicatorValue` | Estimate slippage (bps) |

---

## REST API

| Method | Path | Description |
|--------|------|-------------|
| `GET`  | `/api/blocks` | List all registered block types |
| `GET`  | `/api/blocks/{key}` | Get single block metadata |
| `GET`  | `/health` | Health check (liveness probe) |

---

## Running

```bash
# Development
dotnet run --project src/modules/designer/MLS.Designer

# Docker
docker build -f src/modules/designer/Dockerfile -t mls-designer src
docker run -p 5250:5250 -p 6250:6250 mls-designer

# Health check
curl http://localhost:5250/health
```

---

## Tests

```bash
dotnet test src/modules/designer/MLS.Designer.Tests -c Release
```

**TradingBlockTests** — 19 tests covering:
- RSI Wilder's algorithm with known fixture data
- RSI warm-up period (no signal during first `period` candles)
- RSI reset behaviour
- All-up / all-down RSI boundary conditions (periods 7, 14, 21)
- MACD output after slow + signal period
- Bollinger position in [0, 1] range
- ATR positive value after warm-up
- VWAP output per candle
- VolumeProfile percentile in [0, 1]
- BlockRegistry.GetAll returns all 28 block types
- BlockRegistry.GetByKey metadata fields
- BlockRegistry.CreateInstance returns new unique instances
