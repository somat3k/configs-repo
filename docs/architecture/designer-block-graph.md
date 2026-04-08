> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Designer Block Graph — MLS Platform

> **Reference**: [Giga-Scale Plan](giga-scale-plan.md) | [Session Schedule](../session-schedule.md) (Sessions 04–07)

---

## Block Type Hierarchy

```
IBlockElement (MLS.Core.Designer)
│
├── DataSourceBlock            ← IExchangeAdapter / DataLayer subscriber
│   ├── CandleFeedBlock        OHLCV stream from DataLayer (symbol, timeframe)
│   ├── OrderBookFeedBlock     L2 depth updates (levels parameter)
│   ├── TradeFeedBlock         Tick-by-tick trades
│   ├── OnChainFeedBlock       Arbitrum event logs via Nethereum
│   └── BacktestReplayBlock    Historical OHLCV replay from PostgreSQL
│
├── IndicatorBlock             ← BaseIndicator.OnProcess() equivalent
│   ├── RSIBlock               14-period RSI → normalised [0, 1]
│   ├── MACDBlock              fast(12)/slow(26)/signal(9) line
│   ├── BollingerBlock         position within bands [0, 1]
│   ├── ATRBlock               average true range (volatility proxy)
│   ├── VWAPBlock              volume-weighted average price
│   ├── VolumeProfileBlock     StockSharp VolumeProfileIndicator equivalent
│   └── CustomIndicatorBlock   Roslyn-compiled user C# (Phase 6)
│
├── MLBlock                    ← ONNX Runtime inference block
│   ├── ModelTInferenceBlock   8-feature vector → BUY/SELL/HOLD + confidence
│   ├── ModelAInferenceBlock   Exchange + path features → arb opportunity score
│   ├── ModelDInferenceBlock   Protocol + yield features → DeFi move prediction
│   └── EnsembleBlock          Weighted vote over multiple active models
│
├── StrategyBlock              ← Composition of above blocks
│   ├── MomentumStrategyBlock  Price momentum + volume confirmation
│   ├── MeanReversionBlock     Z-score entry / exit on Bollinger deviation
│   ├── TrendFollowBlock       Moving average crossover (fast/slow EMA)
│   └── CompositeStrategyBlock ICompositionGraph: nestable (fractal)
│                              disconnected inner sockets → exposed outer ports
│
├── ArbitrageBlock
│   ├── SpreadCalculatorBlock  exchange_A_price − exchange_B_price (bp normalised)
│   ├── nHOPPathFinderBlock    BFS/Bellman-Ford on token-exchange directed graph
│   ├── FlashLoanBlock         Flash loan initiation via Aave-compatible interface
│   └── ProfitGateBlock        Pass-through gate: profit > gas_estimate + min_spread
│
├── DeFiBlock
│   ├── MorphoSupplyBlock      Lend asset at optimal market rate
│   ├── MorphoBorrowBlock      Borrow against collateral with HF monitoring
│   ├── BalancerSwapBlock      AMM swap execution via Vault interface
│   ├── CollateralHealthBlock  Streaming health factor updates (alert at HF < 1.2)
│   ├── YieldOptimizerBlock    Auto-rebalance across Morpho + Balancer protocols
│   └── LiquidationGuardBlock  Emergency close all positions if HF < threshold
│
├── MLTrainingBlock            ← Training as a composable block graph (Phase 1, Session 06)
│   ├── DataLoaderBlock        Load feature batches from FeatureStore (PostgreSQL)
│   ├── FeatureEngineerBlock   Compute RSI/MACD/BB/ATR/VWAP/Volume/Spread
│   ├── TrainSplitBlock        80/10/10 train/val/test split with stratification
│   ├── TrainModelBlock        Dispatch TRAINING_JOB_START → Shell VM Python
│   ├── ValidateModelBlock     Receive metrics: F1, AUC, Sharpe from Shell VM
│   ├── ExportONNXBlock        Trigger ONNX export + IPFS upload + registry entry
│   └── HyperparamSearchBlock  Optuna Bayesian search (TPE sampler, Hyperband pruner)
│
├── RiskBlock
│   ├── PositionSizerBlock     Kelly criterion / fixed fraction of equity
│   ├── StopLossBlock          Trailing stop (ATR multiple) or fixed percentage
│   ├── DrawdownGuardBlock     Halt strategy if daily/weekly drawdown exceeded
│   └── ExposureLimitBlock     Total portfolio exposure cap per asset class
│
├── ExecutionBlock
│   ├── OrderEmitterBlock      Emit TRADE_SIGNAL → Block Controller → Broker
│   ├── OrderRouterBlock       Smart routing: HYPERLIQUID primary / Camelot fallback
│   ├── FillTrackerBlock       Await ORDER_RESULT, retry on partial fill / rejection
│   └── SlippageEstimatorBlock Predict fill price vs. signal price from order book depth
│
└── AnalyticsBlock             ← IAnalyticsScript equivalent (StockSharp)
    ├── ChartExportBlock       Emit OHLCV + signal data → TradingChart MDI panel
    ├── PythonScriptBlock      Execute Python in Shell VM, stream output to canvas
    ├── SHAPExplainerBlock     SHAP feature importance → ApexCharts bar chart in canvas
    └── ReportBlock            P&L report → PnLReport MDI panel
```

---

## Socket Type System

### BlockSocketType Enum

```csharp
public enum BlockSocketType
{
    // Data source outputs
    CandleStream,           // OHLCVCandle — blue
    OrderBookUpdate,        // OrderBookLevel2 — dark blue
    TradeTickStream,        // TradeTick — navy
    OnChainEvent,           // ArbitrumEvent — indigo

    // Indicator outputs
    IndicatorValue,         // float (normalised) — cyan
    IndicatorSeries,        // float[] (window) — light cyan

    // ML outputs
    MLSignal,               // ModelSignal { direction, confidence } — purple
    MLProbabilities,        // Dictionary<string, float> — light purple

    // Risk outputs
    RiskDecision,           // RiskGate { allow, reason, quantity } — orange
    ExposureUpdate,         // ExposureReport — amber

    // Trading outputs
    TradeOrder,             // OrderRequest — green
    OrderResult,            // FillConfirmation — lime

    // Arbitrage outputs
    ArbitrageOpportunity,   // ArbOpp { spread, path, profit_usd } — yellow
    PathUpdate,             // TokenPath (nHOP graph update) — gold

    // DeFi outputs
    DeFiSignal,             // YieldMove { protocol, action } — teal
    HealthFactorUpdate,     // HealthFactor { value, status } — mint

    // Training outputs
    TrainingStatus,         // TrainResult { metrics, path } — pink
    FeatureVector,          // float[] (model input features) — rose

    // Analytics outputs
    ChartData,              // ChartUpdate { type, series } — white
    ReportData,             // Report { type, period, data } — light grey
}
```

### Socket Compatibility Matrix

| From Socket Type | Compatible Destination Blocks |
|---|---|
| `CandleStream` | RSIBlock, MACDBlock, BollingerBlock, ATRBlock, VWAPBlock, VolumeProfileBlock, BacktestReplayBlock, FeatureEngineerBlock, FeedSourceBlock |
| `IndicatorValue` | ModelTInferenceBlock, EnsembleBlock, MomentumStrategyBlock, MeanReversionBlock |
| `MLSignal` | PositionSizerBlock, StopLossBlock, DrawdownGuardBlock, OrderEmitterBlock, ChartExportBlock |
| `RiskDecision` | OrderEmitterBlock, OrderRouterBlock |
| `TradeOrder` | FillTrackerBlock |
| `OrderResult` | PositionSizerBlock (feedback), ReportBlock |
| `ArbitrageOpportunity` | ProfitGateBlock, FlashLoanBlock |
| `DeFiSignal` | MorphoSupplyBlock, MorphoBorrowBlock, BalancerSwapBlock |
| `TrainingStatus` | ExportONNXBlock, ValidateModelBlock |
| `FeatureVector` | ModelTInferenceBlock, ModelAInferenceBlock, ModelDInferenceBlock |
| `ChartData` | Canvas MDI panel (TradingChart, PnLReport) |

---

## CompositeStrategyBlock (Fractal Nesting)

Based on StockSharp's `CompositionDiagramElement` pattern:

```
CompositeStrategyBlock
└── Internal graph: { RSIBlock → MLBlock → RiskBlock → OrderEmitter }
    Inner sockets disconnected from outside:
      - RSIBlock.InputSockets[0] (CandleStream) → exposed as outer Input socket
      - OrderEmitter.OutputSockets[0] (TradeOrder) → exposed as outer Output socket
    Internal connections hidden from outer graph
    
Outer view of CompositeStrategyBlock:
  Input:  [CandleStream]     ← from CandleFeedBlock in parent graph
  Output: [TradeOrder]       ← to FillTrackerBlock in parent graph
```

### ICompositionGraph Interface

```csharp
public interface ICompositionGraph
{
    Guid GraphId { get; }
    string Name { get; }
    int SchemaVersion { get; }               // Increment on every structural change

    IReadOnlyList<IBlockElement> Blocks { get; }
    IReadOnlyList<BlockConnection> Connections { get; }

    /// Disconnected inner sockets become external ports of this composition block
    IReadOnlyList<IBlockSocket> GetExposedPorts();

    Task AddBlockAsync(IBlockElement block, CancellationToken ct);
    Task RemoveBlockAsync(Guid blockId, CancellationToken ct);
    Task ConnectAsync(Guid fromSocketId, Guid toSocketId, CancellationToken ct);
    Task DisconnectAsync(Guid connectionId, CancellationToken ct);

    Task DeployAsync(CancellationToken ct);     // Emit STRATEGY_DEPLOY
    Task StopAsync(CancellationToken ct);       // Emit STRATEGY_STATE_CHANGE(stopped)
    Task BacktestAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
```

---

## BlockParameter System

All block configuration uses typed parameters (equivalent to StockSharp's `StrategyParam<T>`):

```csharp
public abstract record BlockParameter(string Name, string DisplayName, string Description);

public sealed record BlockParameter<T>(
    string Name,
    string DisplayName,
    string Description,
    T DefaultValue,
    T? MinValue = default,
    T? MaxValue = default,
    bool IsOptimizable = false     // Included in HyperparamSearchBlock
) : BlockParameter(Name, DisplayName, Description);

// Examples:
// RSIBlock parameters:
new BlockParameter<int>("Period", "Period", "RSI lookback period", 14, 2, 100, true)
new BlockParameter<Level1Fields?>("Source", "Source", "Price field", Level1Fields.ClosePrice)

// ModelTInferenceBlock parameters:
new BlockParameter<string>("ModelId", "Model", "Model registry ID", "model-t")
new BlockParameter<float>("ConfidenceThreshold", "Min Confidence", "Minimum confidence to emit signal", 0.7f, 0.0f, 1.0f, true)
```

---

## Strategy Schema (JSON Persistence)

All strategies serialized as versioned JSON for PostgreSQL storage and IPFS distribution:

```json
{
  "graph_id": "550e8400-e29b-41d4-a716-446655440001",
  "name": "BTC Momentum Strategy",
  "description": "Momentum-driven BTC trading using model-t signals",
  "schema_version": 3,
  "domain": "Trading",
  "created_at": "2026-01-15T10:00:00Z",
  "blocks": [
    {
      "block_id": "a1b2c3d4-...",
      "block_type": "CandleFeedBlock",
      "position": { "x": 100, "y": 150 },
      "parameters": {
        "symbol": "BTC-PERP",
        "timeframe": "5m",
        "exchange": "hyperliquid"
      }
    },
    {
      "block_id": "b2c3d4e5-...",
      "block_type": "RSIBlock",
      "position": { "x": 300, "y": 100 },
      "parameters": { "period": 14, "source": "ClosePrice" }
    },
    {
      "block_id": "c3d4e5f6-...",
      "block_type": "ModelTInferenceBlock",
      "position": { "x": 500, "y": 150 },
      "parameters": { "model_id": "model-t", "confidence_threshold": 0.75 }
    }
  ],
  "connections": [
    {
      "connection_id": "conn-001",
      "from_block_id": "a1b2c3d4-...",
      "from_socket": "candle_output",
      "to_block_id": "b2c3d4e5-...",
      "to_socket": "candle_input"
    }
  ]
}
```

---

## Block Lifecycle

```
1. REGISTRATION    BlockRegistry.RegisterType<RSIBlock>() on module startup
2. INSTANTIATION   BlockRegistry.Create("RSIBlock", parameters) → IBlockElement
3. DEPLOYMENT      ICompositionGraph.DeployAsync() → STRATEGY_DEPLOY envelope
4. LIVE EXECUTION  BlockElement.ProcessAsync(signal, ct) called on each incoming signal
5. PRELOADING      BlockElement.PreloadAsync(historicalData, ct) for backtest warmup
6. RESET           BlockElement.Reset() clears internal state (equivalent to BaseIndicator.Reset())
7. STOP            STRATEGY_STATE_CHANGE(stopped) → all blocks stop processing
8. DISPOSE         IAsyncDisposable.DisposeAsync() releases resources
```

---

## Pre-Built Strategy Templates

Stored in `designer-templates/` as JSON, loadable via `POST /api/strategies/from-template/{name}`:

| Template | Domain | Blocks | Description |
|----------|--------|--------|-------------|
| `momentum-long.json` | Trading | CandleFeed → RSI → MACD → ModelT → RiskBlock → OrderEmitter | Long-only momentum with ML confirmation |
| `mean-reversion.json` | Trading | CandleFeed → Bollinger → VWAP → ModelT → RiskBlock → OrderEmitter | Mean reversion on BB deviation |
| `camelot-dfyn-spread.json` | Arbitrage | Camelot+DFYN feeds → SpreadCalc → ProfitGate → OrderEmitter | 2-exchange spread capture |
| `nhop-3leg-arb.json` | Arbitrage | Multi-exchange feeds → nHOPFinder → FlashLoan → ProfitGate | 3-hop flash loan arbitrage |
| `morpho-yield-optimizer.json` | DeFi | MorphoSupply → CollateralHealth → YieldOptimizer → LiquidGuard | Lending yield optimization |
| `balancer-rebalance.json` | DeFi | BalancerSwap → CollateralHealth → LiquidGuard | AMM pool rebalancing |
| `model-t-full-pipeline.json` | ML Training | DataLoader → FeatureEngineer → TrainSplit → TrainModel → Validate → ExportONNX | Full model-t training |
| `model-a-retrain.json` | ML Training | DataLoader → FeatureEngineer → TrainSplit → TrainModel → Validate → ExportONNX | Full model-a training |
