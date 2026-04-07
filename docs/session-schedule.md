# MLS Platform — Hyper-Detailed Session Schedule

> **Reference document for all implementation phases.**
> Each session is a GitHub Copilot context window with a specific, achievable objective.
> Sessions are designed to be atomic: each produces runnable, tested code.
> Sessions within the same phase can be started in parallel if dependencies allow.

---

## Phase Map

| Phase | Sessions | Theme | Depends On |
|-------|----------|-------|-----------|
| **0** | 1 – 3   | Foundation: Core contracts + Block Controller extensions | — |
| **1** | 4 – 7   | Designer Module: block graph, all domains, execution | Phase 0 |
| **2** | 8 – 10  | AI Hub: providers, SK plugins, canvas dispatch | Phase 0 |
| **3** | 11 – 14 | MDI Canvas Rewrite: Blazor designer UI, chat panel, all panels | Phase 1, 2 |
| **4** | 15 – 16 | Hydra Data Collection: exchange feeds, gap detection | Phase 1 |
| **5** | 17 – 18 | PWA + Chrome Extension + Mobile | Phase 3 |
| **6** | 19 – 20 | Dynamic Compilation: Roslyn strategy compiler, IPFS loading | Phase 1 | ✅ Session 19 complete |
| **7** | 21 – 22 | Visualization + Benchmarks: live canvas pulses, BDN suite | Phase 3, 6 |

---

## Phase 0 — Foundation

### Session 01 — Core Block Contracts + Extended Envelope Types

**Objective**: Establish `MLS.Core.Designer` contracts and extend the Envelope Protocol with all new message types for the Designer, AI Hub, Hydra, and Training domains.

**Files to Create / Modify**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/core/MLS.Core/Designer/IBlockElement.cs` | Root block element interface |
| CREATE | `src/core/MLS.Core/Designer/IBlockSocket.cs` | Typed socket interface |
| CREATE | `src/core/MLS.Core/Designer/BlockSocketType.cs` | Enum: all socket data types |
| CREATE | `src/core/MLS.Core/Designer/ICompositionGraph.cs` | Nestable composition graph |
| CREATE | `src/core/MLS.Core/Designer/BlockParameter.cs` | Generic typed block parameter |
| CREATE | `src/core/MLS.Core/Constants/MessageTypes.Designer.cs` | STRATEGY_*, AI_*, TRAINING_*, DATA_* constants |
| CREATE | `src/core/MLS.Core/Contracts/Designer/` | All new typed payload records |
| MODIFY | `docs/payload-schemas.md` | Add all new payload schemas |

**Key Interfaces to Implement**

```csharp
// IBlockElement — single processing node
public interface IBlockElement
{
    Guid BlockId { get; }
    string BlockType { get; }               // e.g. "RSIBlock", "ModelTInferenceBlock"
    IReadOnlyList<IBlockSocket> InputSockets { get; }
    IReadOnlyList<IBlockSocket> OutputSockets { get; }
    IReadOnlyList<BlockParameter> Parameters { get; }
    ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct);
}

// IBlockSocket — typed connection point
public interface IBlockSocket
{
    Guid SocketId { get; }
    string Name { get; }
    BlockSocketType DataType { get; }
    SocketDirection Direction { get; }      // Input | Output
    bool IsConnected { get; }
    Guid? ConnectedToSocketId { get; }
}

// ICompositionGraph — nestable strategy container
public interface ICompositionGraph
{
    Guid GraphId { get; }
    string Name { get; }
    int SchemaVersion { get; }
    IReadOnlyList<IBlockElement> Blocks { get; }
    IReadOnlyList<BlockConnection> Connections { get; }
    IReadOnlyList<IBlockSocket> GetExposedPorts();   // disconnected inner sockets → outer
    Task AddBlockAsync(IBlockElement block, CancellationToken ct);
    Task ConnectAsync(Guid fromSocketId, Guid toSocketId, CancellationToken ct);
    Task DisconnectAsync(Guid connectionId, CancellationToken ct);
}
```

**New Envelope Message Types**

```
STRATEGY_DEPLOY          block-controller → route strategy graph to subscription table
STRATEGY_STATE_CHANGE    block-controller → broadcast (running/stopped/backtesting)
BLOCK_SIGNAL             block → next block in connected graph
TRAINING_JOB_START       designer → shell-vm (kick off Python training)
TRAINING_JOB_PROGRESS    shell-vm → designer (epoch metrics stream)
TRAINING_JOB_COMPLETE    shell-vm → designer + ml-runtime (ONNX ready)
AI_QUERY                 web-app → ai-hub (user message + assembled context)
AI_RESPONSE_CHUNK        ai-hub → web-app (streaming SSE chunk)
AI_CANVAS_ACTION         ai-hub → web-app (open panel, update chart, annotate)
DATA_COLLECTION_START    designer → data-layer (start hydra feed job)
DATA_GAP_DETECTED        data-layer → designer/web-app (gap notification)
DATA_GAP_FILLED          data-layer → designer/web-app (backfill complete)
EXCHANGE_PRICE_UPDATE    exchange-adapter → arbitrage-scanner (per-exchange tick)
ARB_PATH_FOUND           arbitrage-scanner → designer + broker (nHOP result)
DEFI_HEALTH_WARNING      defi → designer + ai-hub (health factor alert)
CANVAS_LAYOUT_SAVE       web-app → block-controller (MDI layout persistence)
```

**Skills to Apply**
- `.skills/dotnet-devs.md`
- `.skills/system-architect.md`
- `.skills/designer.md` ← new skill (created in Session 01)

**Acceptance Criteria**
- [x] All interfaces compile with `net9.0`, nullable enabled, C# 13
- [x] All new `MessageTypes.*.cs` constants referenced in updated `docs/payload-schemas.md`
- [x] `BlockSocketType` enum covers all flow directions from architecture diagram
- [x] xUnit unit test: `BlockConnectionValidatorTests` — verify type-safe socket connections reject mismatched types

---

### Session 02 — Block Controller: Strategy Router + Subscription Table

**Objective**: Extend Block Controller to accept `STRATEGY_DEPLOY` envelopes, parse strategy graphs, and dynamically mutate the subscription table so live envelopes follow designer connections.

**Files to Create / Modify**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/block-controller/MLS.BlockController/Services/StrategyRouter.cs` | Parse StrategyGraph.json → update SubscriptionTable |
| CREATE | `src/block-controller/MLS.BlockController/Services/SubscriptionTable.cs` | Thread-safe topic → [moduleId] mapping |
| CREATE | `src/block-controller/MLS.BlockController/Services/IStrategyRouter.cs` | Interface |
| MODIFY | `src/block-controller/MLS.BlockController/Hubs/BlockControllerHub.cs` | Accept STRATEGY_DEPLOY, CANVAS_LAYOUT_SAVE |
| MODIFY | `src/block-controller/docs/SESSION.md` | Add strategy routing section |

**Key Logic: StrategyRouter**

```csharp
public sealed class StrategyRouter(
    ISubscriptionTable _subscriptions,
    IMessageRouter _router,
    ILogger<StrategyRouter> _logger
) : IStrategyRouter
{
    /// Deploy strategy: for each connection in graph, register subscription
    /// connection (fromBlock.outputSocket.dataType → toBlock.moduleId)
    public async Task DeployAsync(StrategyGraphPayload graph, CancellationToken ct)
    {
        // 1. Validate graph (no cycles, type-safe connections)
        // 2. Clear previous subscriptions for this strategy
        // 3. For each edge (from→to): _subscriptions.AddAsync(topic, toModuleId, ct)
        // 4. Broadcast STRATEGY_STATE_CHANGE(Running)
    }

    public async Task StopAsync(Guid strategyId, CancellationToken ct) { }
    public async Task BacktestAsync(Guid strategyId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) { }
}
```

**Performance Requirements**
- Subscription table lookup: O(1) via `ConcurrentDictionary<string, ImmutableHashSet<Guid>>`
- Envelope routing hot path: zero allocation (ArrayPool, Span)
- All Channel<T> bounded with `BoundedChannelOptions(1024) { FullMode = DropOldest }`

**Skills to Apply**
- `.skills/beast-development.md`
- `.skills/websockets-inferences.md`
- `.skills/dotnet-devs.md`

**Acceptance Criteria**
- [x] `STRATEGY_DEPLOY` envelope processed in < 5ms
- [x] Subscription table correctly routes `BLOCK_SIGNAL` to connected module
- [x] Unit test: `StrategyRouterTests.DeployAsync_RoutesEnvelopesToSubscribedModules`
- [ ] Integration test: round-trip deploy → signal → confirmation using `Aspire.Hosting.Testing`

---

### Session 03 — Core Skills + Copilot Rules

**Objective**: Write three new `.skills/` files and one new copilot rule so all subsequent sessions have correct Copilot context.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `.skills/designer.md` | Block graph patterns, socket types, composition |
| CREATE | `.skills/ai-hub.md` | SK plugin patterns, provider routing, canvas actions |
| CREATE | `.skills/pwa-chrome.md` | PWA manifest, Workbox SW, Chrome MV3 extension |
| CREATE | `.skills/exchange-adapters.md` | IExchangeAdapter, Nethereum, Arbitrum DEX specifics |
| CREATE | `.skills/hydra-collector.md` | Data collection jobs, gap detection, feed scheduling |
| CREATE | `.github/copilot-rules/rule-designer-blocks.md` | Block type rules, socket validation, schema versioning |
| MODIFY | `.github/copilot-instructions.md` | Add Designer, AI Hub, Performance rules sections |

**Acceptance Criteria**
- [x] Each skill file follows the same frontmatter + section structure as existing skills
- [x] `rule-designer-blocks.md` covers: IBlockElement required, socket types, SchemaVersion increment, CompositeBlock nesting
- [x] copilot-instructions.md additions do not break existing sections

---

## Phase 1 — Designer Module

### Session 04 — Designer Module Scaffold + Trading Domain Blocks

**Objective**: Create `MLS.Designer` project with module infrastructure and all Trading-domain blocks (IndicatorBlocks, StrategyBlocks, RiskBlocks, ExecutionBlocks).

**Module Identity**

| Field | Value |
|-------|-------|
| Name | `designer` |
| Namespace | `MLS.Designer` |
| HTTP Port | `5250` |
| WebSocket Port | `6250` |
| Container | `mls-designer` |

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/designer/MLS.Designer/MLS.Designer.csproj` | Project file, net9.0 |
| CREATE | `src/modules/designer/MLS.Designer/Program.cs` | ASP.NET Core host, Block Controller registration |
| CREATE | `src/modules/designer/MLS.Designer/Blocks/Trading/` | All trading blocks (see below) |
| CREATE | `src/modules/designer/MLS.Designer/Services/BlockRegistry.cs` | Central catalog of available block types |
| CREATE | `src/modules/designer/MLS.Designer/Services/IBlockRegistry.cs` | Interface |
| CREATE | `src/modules/designer/MLS.Designer/Hubs/DesignerHub.cs` | SignalR hub |
| CREATE | `src/modules/designer/MLS.Designer/Controllers/BlocksController.cs` | REST: GET /api/blocks, GET /api/blocks/{type} |
| CREATE | `src/modules/designer/README.md` | Module README |
| CREATE | `src/modules/designer/docs/SESSION.md` | Copilot session prompt |

**Trading Domain Block Implementations**

```
Blocks/Trading/
├── DataSourceBlocks/
│   ├── CandleFeedBlock.cs          IBlockElement: subscribes DataLayer CANDLE stream
│   ├── OrderBookFeedBlock.cs       L2 depth updates
│   ├── TradeFeedBlock.cs           Tick-by-tick trades
│   └── BacktestReplayBlock.cs      Historical replay from PostgreSQL
│
├── IndicatorBlocks/
│   ├── RSIBlock.cs                 14-period RSI → normalised [0,1]
│   ├── MACDBlock.cs                fast/slow/signal line output
│   ├── BollingerBlock.cs           position within bands [0,1]
│   ├── ATRBlock.cs                 average true range (volatility)
│   ├── VWAPBlock.cs                volume-weighted avg price
│   ├── VolumeProfileBlock.cs       volume at price distribution
│   └── CustomIndicatorBlock.cs     Roslyn-compiled user C# (Phase 6)
│
├── MLBlocks/
│   ├── ModelTInferenceBlock.cs     ONNX Runtime → BUY/SELL/HOLD + confidence
│   ├── ModelAInferenceBlock.cs     Arbitrage opportunity score
│   ├── ModelDInferenceBlock.cs     DeFi yield prediction
│   └── EnsembleBlock.cs            Weighted vote over multiple models
│
├── StrategyBlocks/
│   ├── MomentumStrategyBlock.cs    Price momentum + volume filter
│   ├── MeanReversionBlock.cs       Z-score mean reversion
│   ├── TrendFollowBlock.cs         Moving average crossover
│   └── CompositeStrategyBlock.cs   ICompositionGraph: nestable
│
├── RiskBlocks/
│   ├── PositionSizerBlock.cs       Kelly criterion / fixed fraction
│   ├── StopLossBlock.cs            Trailing / fixed ATR-based
│   ├── DrawdownGuardBlock.cs       Halt strategy if max DD exceeded
│   └── ExposureLimitBlock.cs       Total portfolio exposure cap
│
└── ExecutionBlocks/
    ├── OrderEmitterBlock.cs        TRADE_SIGNAL → Broker
    ├── OrderRouterBlock.cs         Smart routing: HYPERLIQUID / Camelot
    ├── FillTrackerBlock.cs         Await ORDER_RESULT, retry on rejection
    └── SlippageEstimatorBlock.cs   Predict fill price vs. signal price
```

**Skills to Apply**
- `.skills/designer.md`
- `.skills/machine-learning.md`
- `.skills/beast-development.md`
- `.skills/dotnet-devs.md`

**Acceptance Criteria**
- [x] Module registers with Block Controller on startup (MODULE_REGISTER)
- [x] Heartbeat every 5 seconds (MODULE_HEARTBEAT)
- [x] `BlockRegistry.GetAll()` returns all Trading domain blocks with metadata
- [x] `RSIBlock.ProcessAsync` produces correct RSI for known OHLCV fixture data
- [x] `ModelTInferenceBlock.ProcessAsync` calls ML Runtime HTTP inference in < 15ms
- [x] xUnit: `TradingBlockTests` — all indicator blocks with known inputs
- [x] Docker: `mls-designer` container starts on ports 5250/6250

---

### Session 05 — Arbitrage + DeFi Domain Blocks + Exchange Adapters

**Objective**: Implement all Arbitrage and DeFi blocks plus the four Arbitrum exchange adapters.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/designer/MLS.Designer/Blocks/Arbitrage/` | All arbitrage blocks |
| CREATE | `src/modules/designer/MLS.Designer/Blocks/DeFi/` | All DeFi blocks |
| CREATE | `src/modules/designer/MLS.Designer/Exchanges/` | Exchange adapters |

**Arbitrage Domain Blocks**

```
Blocks/Arbitrage/
├── SpreadCalculatorBlock.cs        exchange A price − exchange B price
├── nHOPPathFinderBlock.cs          BFS/Bellman-Ford on token-exchange graph
├── FlashLoanBlock.cs               Flash loan initiation (Aave-compatible)
└── ProfitGateBlock.cs              Pass only if spread > threshold + gas
```

**DeFi Domain Blocks**

```
Blocks/DeFi/
├── MorphoSupplyBlock.cs            Lend asset at optimal rate
├── MorphoBorrowBlock.cs            Borrow against collateral
├── BalancerSwapBlock.cs            AMM swap execution
├── CollateralHealthBlock.cs        Health factor monitor (stream HF updates)
├── YieldOptimizerBlock.cs          Auto-rebalance across protocols
└── LiquidationGuardBlock.cs        Emergency close if HF < threshold
```

**Exchange Adapters**

```
Exchanges/
├── IExchangeAdapter.cs             Interface: GetPrice, GetOrderBook, Subscribe, ExecuteSwap
├── HyperliquidAdapter.cs           REST + WS: perps BTC/ETH/ARB
├── CamelotAdapter.cs               Arbitrum AMM + concentrated liquidity
├── DFYNAdapter.cs                  Cross-chain DEX on Arbitrum
├── BalancerAdapter.cs              Weighted AMM pools
├── MorphoAdapter.cs                Lending protocol (supply/borrow/repay)
└── ExchangeRegistry.cs             PostgreSQL-backed address book
```

**nHOP Path Finder Algorithm**

```
Tokens: [WETH, USDC, ARB, WBTC, GMX, RDNT]
Exchanges: [Camelot, DFYN, Balancer, Morpho]

Step 1: Build directed graph
  nodes = tokens × exchanges
  edges = liquidity pool (buy_price, sell_price, liquidity, gas_estimate)

Step 2: BFS depth ≤ 4 hops starting from input token
  At each hop: enumerate all available pools
  
Step 3: Score path
  profit = output_amount - input_amount - sum(gas_costs)
  if profit > 0: add to candidate list

Step 4: Rank by profit/capital ratio
  Emit ARB_PATH_FOUND envelope for top 3 paths
```

**Skills to Apply**
- `.skills/designer.md`
- `.skills/web3.md`
- `.skills/exchange-adapters.md` ← new skill

**Acceptance Criteria**
- [x] `nHOPPathFinderBlock` correctly finds 3-hop arbitrage in unit test with mock prices
- [x] `MorphoSupplyBlock` emits `DeFiSignal` payload with correct protocol + action
- [x] `HyperliquidAdapter` connects to HYPERLIQUID WS and streams prices
- [x] `ExchangeRegistry` loads all addresses from PostgreSQL at startup, never hardcoded
- [x] All blockchain addresses use `BlockchainAddress` enum from `MLS.Core.Constants`

**Session Status: ✅ COMPLETE** — Implemented 2026-04-03

---

### Session 06 — ML Training Domain Blocks + Training Pipeline

**Objective**: Implement the ML Training composer domain — training as composable blocks that dispatch to Shell VM and stream progress back to the canvas.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/designer/MLS.Designer/Blocks/MLTraining/` | All training pipeline blocks |
| CREATE | `src/modules/designer/MLS.Designer/Services/TrainingDispatcher.cs` | Dispatch training jobs to Shell VM |
| CREATE | `src/modules/ml-runtime/scripts/training_pipeline.py` | Unified Python training entry point |

**ML Training Domain Blocks**

```
Blocks/MLTraining/
├── DataLoaderBlock.cs              Load features from FeatureStore (PostgreSQL)
├── FeatureEngineerBlock.cs         Compute RSI/MACD/BB/ATR/VWAP/Volume/Spread
├── TrainSplitBlock.cs              80/10/10 train/val/test split
├── TrainModelBlock.cs              Dispatch TRAINING_JOB_START to Shell VM
├── ValidateModelBlock.cs           Receive metrics from Shell VM stream
├── ExportONNXBlock.cs              Trigger ONNX export + IPFS upload
└── HyperparamSearchBlock.cs        Grid/random/Bayesian search loop (Optuna)
```

**Training Job Lifecycle**

```
TrainModelBlock.ProcessAsync()
  1. Build TRAINING_JOB_START envelope:
     { model_type, feature_schema, hyperparams, epochs, batch_size }
  2. Emit via Envelope → Block Controller → Shell VM
  3. Shell VM spawns: python training_pipeline.py --config <json>
  4. Shell VM streams TRAINING_JOB_PROGRESS every epoch:
     { epoch, total_epochs, train_loss, val_loss, accuracy, elapsed_ms }
  5. On completion: Shell VM emits TRAINING_JOB_COMPLETE:
     { model_id, onnx_path, joblib_path, ipfs_cid, metrics }
  6. ExportONNXBlock receives → registers in ml_registry table
```

**Python Training Script (`training_pipeline.py`)**

```python
# Entrypoint used by Shell VM for all three models (model-t, model-a, model-d)
# L1: torch.compile(mode="reduce-overhead", fullgraph=True)
# L2: DataLoader(num_workers=4, pin_memory=True)
# L3: DistributedDataParallel via Aspire replica detection
# L4: AMP mixed precision (torch.cuda.amp.autocast, bf16)
```

**Skills to Apply**
- `.skills/machine-learning.md`
- `.skills/models/model-t.md`
- `.skills/models/model-a.md`
- `.skills/models/model-d.md`
- `.skills/acceleration/acceleration.md`

**Acceptance Criteria**
- [x] `TrainModelBlock` emits `TRAINING_JOB_START` with correct feature schema for each model type
- [x] `training_pipeline.py` runs with `--dry-run` flag completing in < 30s
- [x] Shell VM streams at least one `TRAINING_JOB_PROGRESS` envelope per epoch
- [x] `ExportONNXBlock` uploads ONNX to IPFS and records CID in PostgreSQL
- [x] Training job completes end-to-end in test environment (model_t, 5 epochs)

**Session Status: ✅ COMPLETE** — Implemented 2026-04-05

---

### Session 07 — Universal Tile Builder + Transformation Controller + Hydra Data Domain

**Objective**: Introduce the `ICustomTile` abstraction layer for user-composable indicator rules, the `TransformationController` framework for unified sub-division routing, the `IActionTile` (Block-as-ONE) data source pattern, and complete the Data Hydra domain blocks.

> **Architecture references**:
> - [Universal Tile Builder](architecture/session-07-universal-tile-builder.md) — `ICustomTile`, `ITileRule` DSL, dynamic socket registration
> - [Label Schema](architecture/session-07-label-schema.md) — Multi-dimensional label tensors for arbitrage navigation
> - [Block Action Tiles](architecture/session-07-block-action-tiles.md) — `IActionTile` Block-as-ONE singleton data source pattern
> - [Transformation Controller](architecture/transformation-controller.md) — Unified payload with `TransformationUnit` audit trail

**Objective (original)**: Complete the Data Hydra domain blocks, implement `StrategyRepository`, and deploy JSON template library.

**Files to Create (Universal Tile Builder + Transformation Controller)**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/core/MLS.Core/Designer/ICustomTile.cs` | User-composable tile interface |
| CREATE | `src/core/MLS.Core/Designer/ITileRule.cs` | Rule + condition + action interfaces |
| CREATE | `src/core/MLS.Core/Designer/ITileCondition.cs` | Condition evaluator contract |
| CREATE | `src/core/MLS.Core/Designer/ITileAction.cs` | Action executor contract |
| CREATE | `src/core/MLS.Core/Designer/IActionTile.cs` | Block-as-ONE autonomous data source interface |
| CREATE | `src/core/MLS.Core/Designer/TransformationUnit.cs` | Per-block transformation descriptor |
| CREATE | `src/core/MLS.Core/Designer/TransformationEnvelope.cs` | Signal + transformation history |
| CREATE | `src/core/MLS.Core/Designer/ITransformationController.cs` | TC routing interface |
| CREATE | `src/core/MLS.Core/Designer/LabelSchema.cs` | Multi-dimensional label schema record |
| CREATE | `src/core/MLS.Core/Constants/SubDivision.cs` | Named sub-division constants |
| CREATE | `src/modules/designer/MLS.Designer/Blocks/CustomTiles/CustomIndicatorTile.cs` | Concrete `ICustomTile` + rule engine |
| CREATE | `src/modules/designer/MLS.Designer/Blocks/CustomTiles/PassThroughTile.cs` | Identity tile |
| CREATE | `src/modules/designer/MLS.Designer/Services/TileRuleEngine.cs` | Rule evaluation engine |
| CREATE | `src/modules/designer/MLS.Designer/Services/TransformationController.cs` | TC implementation |
| MODIFY | `src/modules/designer/MLS.Designer/Blocks/DeFi/LendingHealthBlock.cs` | Implement `IActionTile` |
| MODIFY | `src/modules/designer/MLS.Designer/Blocks/BlockBase.cs` | Add `EmitTransformedAsync` |
| CREATE | `docs/architecture/session-07-universal-tile-builder.md` | ✅ Added (this PR) |
| CREATE | `docs/architecture/session-07-label-schema.md` | ✅ Added (this PR) |
| CREATE | `docs/architecture/session-07-block-action-tiles.md` | ✅ Added (this PR) |
| CREATE | `docs/architecture/transformation-controller.md` | ✅ Added (this PR) |

**Files to Create (Hydra Data Domain + Strategy Persistence)**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/designer/MLS.Designer/Blocks/DataHydra/` | All data collection blocks |
| CREATE | `src/modules/designer/MLS.Designer/Persistence/StrategyRepository.cs` | EF Core: CRUD for strategy schemas |
| CREATE | `src/modules/designer/MLS.Designer/Persistence/StrategySchema.cs` | EF entity |
| CREATE | `designer-templates/trading/momentum-long.json` | Pre-built strategy template |
| CREATE | `designer-templates/trading/mean-reversion.json` | Pre-built strategy template |
| CREATE | `designer-templates/arbitrage/camelot-dfyn-spread.json` | Pre-built arb template |
| CREATE | `designer-templates/arbitrage/nhop-3leg-arb.json` | Pre-built nHOP template |
| CREATE | `designer-templates/defi/morpho-yield-optimizer.json` | Pre-built DeFi template |
| CREATE | `designer-templates/ml-training/model-t-full-pipeline.json` | Full training template |
| MODIFY | `src/modules/designer/MLS.Designer/Controllers/StrategiesController.cs` | REST CRUD |


**Data Hydra Domain Blocks**

```
Blocks/DataHydra/
├── FeedSourceBlock.cs          Subscribe exchange feed (exchange, symbol, datatype)
├── FilterBlock.cs              Filter candles (symbol, timeframe, min_volume)
├── NormalisationBlock.cs       OHLCV normalisation to standard schema
├── RouterBlock.cs              Route to chart panel OR to strategy graph
├── BackfillBlock.cs            Trigger REST backfill for date range
└── GapMonitorBlock.cs          Monitor for data gaps, emit DATA_GAP_DETECTED
```

**Strategy REST API**

```
GET    /api/strategies                 List all strategies (name, type, status)
GET    /api/strategies/{id}            Get strategy schema JSON
POST   /api/strategies                 Create from graph schema
PUT    /api/strategies/{id}            Update schema (increments SchemaVersion)
DELETE /api/strategies/{id}            Soft-delete
POST   /api/strategies/{id}/deploy     Emit STRATEGY_DEPLOY to Block Controller
POST   /api/strategies/{id}/stop       Emit STRATEGY_STATE_CHANGE(stopped)
POST   /api/strategies/{id}/backtest   Emit STRATEGY_STATE_CHANGE(backtesting) + params
GET    /api/templates                  List all JSON templates
POST   /api/strategies/from-template/{name}  Create strategy from template
```

**Skills to Apply**
- `.skills/designer.md`
- `.skills/hydra-collector.md` ← new skill
- `.skills/storage-data-management.md`

**Acceptance Criteria**
- [x] `StrategyRepository.CreateAsync` persists and rehydrates a full strategy graph
- [x] `momentum-long.json` template loads and deploys without validation errors
- [x] `FeedSourceBlock` connects to DataLayer WebSocket and receives candle stream
- [x] `GapMonitorBlock` detects a synthetic 2h gap in test fixture and emits `DATA_GAP_DETECTED`

**Session Status: ✅ COMPLETE** — Implemented 2026-04-05

---

## Phase 2 — AI Hub

### Session 08 — AI Hub Module Scaffold + Provider Router

**Objective**: Create `MLS.AIHub` module with all six LLM provider implementations and the user-defined distributor.

**Module Identity**

| Field | Value |
|-------|-------|
| Name | `ai-hub` |
| Namespace | `MLS.AIHub` |
| HTTP Port | `5750` |
| WebSocket Port | `6750` |
| Container | `mls-ai-hub` |

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/ai-hub/MLS.AIHub/MLS.AIHub.csproj` | Project file, SK packages |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Program.cs` | Host + SK kernel registration |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/ILLMProvider.cs` | Semantic Kernel IChatCompletionService wrapper |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/OpenAIProvider.cs` | GPT-4o, GPT-4-turbo, o3 |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/AnthropicProvider.cs` | Claude 3.5 Sonnet, Claude 3 Opus |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/GoogleProvider.cs` | Gemini 2.5 Pro, Gemini Flash |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/GroqProvider.cs` | Llama3-70b, Mixtral (fast) |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/OpenRouterProvider.cs` | Unified 100+ model routing |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/VercelAIProvider.cs` | AI SDK compatible edge |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Providers/LocalProvider.cs` | Ollama / llama.cpp |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Services/ProviderRouter.cs` | User-defined distributor |
| CREATE | `src/modules/ai-hub/README.md` | Module README |
| CREATE | `src/modules/ai-hub/docs/SESSION.md` | Copilot session prompt |

**Provider Router Logic**

```csharp
public sealed class ProviderRouter(
    IEnumerable<ILLMProvider> _providers,
    IUserPreferenceRepository _prefs,
    ILogger<ProviderRouter> _logger
) : IProviderRouter
{
    // Fallback chain: Primary → Secondary → Local
    // Selection persisted in PostgreSQL user_prefs per user
    // Supports per-request provider override via AI_QUERY.provider_override
    public ILLMProvider SelectProvider(AIQueryPayload query, Guid userId) { }
}
```

**Skills to Apply**
- `.skills/ai-hub.md` ← new skill
- `.skills/semantic-kernel.md`
- `.skills/agents.md`
- `.skills/dotnet-devs.md`

**Acceptance Criteria**
- [x] All 7 providers implement `ILLMProvider` and are registered in DI
- [x] `ProviderRouter` falls back to Local provider when primary is unavailable
- [x] Provider selection persisted in PostgreSQL and restored on startup
- [x] Module registers with Block Controller and sends heartbeat
- [x] Unit test: `ProviderRouterTests.SelectProvider_FallsBackToLocalWhenPrimaryUnavailable`

**Session Status: ✅ COMPLETE** — Implemented 2026-04-05

---

### Session 09 — Semantic Kernel Plugins (All Domains)

**Objective**: Implement all five Semantic Kernel plugin groups that give the AI chat complete awareness and tool access.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/ai-hub/MLS.AIHub/Plugins/TradingPlugin.cs` | KernelFunctions: positions, signals, P&L, order |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Plugins/DesignerPlugin.cs` | KernelFunctions: create/modify/explain strategies |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Plugins/AnalyticsPlugin.cs` | KernelFunctions: plot chart, SHAP, report |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Plugins/MLRuntimePlugin.cs` | KernelFunctions: train, metrics, deploy model |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Plugins/DeFiPlugin.cs` | KernelFunctions: health, simulate rebalance, APYs |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Context/ContextAssembler.cs` | Assembles live project snapshot as SK context |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Context/ProjectSnapshot.cs` | Typed snapshot: modules, positions, strategies |

**Plugin Function Signatures**

```csharp
// TradingPlugin
[KernelFunction, Description("Get all open trading positions with current P&L")]
public async Task<string> GetPositions([Description("Symbol filter")] string? symbol = null) {}

[KernelFunction, Description("Place a trading order on the configured exchange")]
public async Task<string> PlaceOrder(string symbol, string side, decimal quantity, decimal? limitPrice = null) {}

[KernelFunction, Description("Get recent ML signal history for a symbol")]
public async Task<string> GetSignalHistory(string symbol, int count = 20) {}

// DesignerPlugin
[KernelFunction, Description("Create a new trading strategy from a template")]
public async Task<string> CreateStrategy(string name, string templateName) {}

[KernelFunction, Description("Add a block to the active strategy canvas")]
public async Task<string> AddBlock(string blockType, string jsonParameters) {}

[KernelFunction, Description("Run backtest on a strategy over a date range")]
public async Task<string> RunBacktest(Guid strategyId, DateTimeOffset from, DateTimeOffset to) {}

// AnalyticsPlugin
[KernelFunction, Description("Open a price chart for a symbol on the canvas")]
public async Task<string> PlotChart(string symbol, string timeframe = "1h") {}

[KernelFunction, Description("Generate SHAP feature importance plot for a model")]
public async Task<string> GenerateSHAP(string modelId) {}
```

**Skills to Apply**
- `.skills/ai-hub.md`
- `.skills/semantic-kernel.md`
- `.skills/agents.md`

**Acceptance Criteria**
- [x] Each plugin has XML documentation on every KernelFunction (required for AI discovery)
- [x] `ContextAssembler` aggregates data from at least 6 sources within 200ms
- [x] `TradingPlugin.GetPositions()` returns live data from Trader module
- [x] `DesignerPlugin.CreateStrategy()` creates and persists a real strategy in Designer
- [x] `AnalyticsPlugin.PlotChart()` emits `AI_CANVAS_ACTION(OpenPanel)` envelope
- [x] Integration test: full SK function call pipeline with mock kernel

**Session Status: ✅ COMPLETE** — Implemented 2026-04-05

---

### Session 10 — Canvas Action Dispatcher + AI Chat Streaming

**Objective**: Implement the AI response pipeline: context → SK → canvas action dispatch → streaming response to web-app.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/ai-hub/MLS.AIHub/Canvas/CanvasActionDispatcher.cs` | Transform SK results → CanvasAction[] |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Canvas/CanvasAction.cs` | Discriminated union of all action types |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Services/ChatService.cs` | Streaming chat: AI_QUERY → chunks via SignalR |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Hubs/AIHub.cs` | SignalR hub for streaming AI responses |
| CREATE | `src/modules/ai-hub/MLS.AIHub/Controllers/ChatController.cs` | POST /api/chat, GET /api/chat/stream (SSE) |

**Canvas Action Types**

```csharp
public abstract record CanvasAction;
public sealed record OpenPanelAction(string PanelType, JsonElement Data) : CanvasAction;
public sealed record UpdateChartAction(Guid ChartId, string SeriesName, double[] Values) : CanvasAction;
public sealed record HighlightBlockAction(Guid BlockId, string Color, int DurationMs) : CanvasAction;
public sealed record ShowDiagramAction(string MermaidSource, string Title) : CanvasAction;
public sealed record AddAnnotationAction(Guid ChartId, DateTimeOffset Time, string Label, string Color) : CanvasAction;
public sealed record OpenDesignerGraphAction(JsonElement StrategySchema) : CanvasAction;
```

**Chat Response Flow**

```
AI_QUERY envelope received
  ↓
ContextAssembler.AssembleAsync() → ProjectSnapshot (< 200ms)
  ↓
ProviderRouter.SelectProvider() → ILLMProvider
  ↓
SK Kernel.InvokeStreamingAsync(query + context)
  ↓
For each chunk:
  - If chunk contains KernelFunction result → CanvasActionDispatcher.Dispatch(result)
  - AI_CANVAS_ACTION → web-app SignalR
  - AI_RESPONSE_CHUNK → web-app SignalR (text chunk)
  ↓
AI_RESPONSE_COMPLETE → web-app SignalR
```

**Acceptance Criteria**
- [x] Streaming response reaches web-app within 200ms of first token
- [x] Canvas actions dispatched before text response completes (parallel)
- [x] `CanvasActionDispatcher` handles all 6 action types
- [x] Integration test: `ChatService` end-to-end with mock SK kernel and mock provider

**Session Status: ✅ COMPLETE** — Implemented 2026-04-05

---

## Phase 3 — MDI Canvas Rewrite

### Session 11 — Window Manager + DesignerCanvas Core

**Objective**: Implement the MDI window manager and the core DesignerCanvas component with block palette, drag-and-drop, and connection drawing.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/Canvas/CanvasHost.razor` | MDI root: manages all open DocumentWindows |
| CREATE | `src/web-app/WebApp/Components/Canvas/DocumentWindow.razor` | Floating, resizable, dockable panel |
| CREATE | `src/web-app/WebApp/Components/Canvas/WindowManager.cs` | Layout state: positions, sizes, z-order |
| CREATE | `src/web-app/WebApp/Components/Canvas/WindowLayoutService.cs` | Persist to localStorage via JS interop |
| CREATE | `src/web-app/WebApp/Components/Designer/DesignerCanvas.razor` | Block graph editor (SVG-based) |
| CREATE | `src/web-app/WebApp/Components/Designer/BlockPalette.razor` | Categorized block type picker |
| CREATE | `src/web-app/WebApp/Components/Designer/PropertyEditor.razor` | Block parameter editor (typed inputs) |
| CREATE | `src/web-app/WebApp/Components/Designer/ConnectionRenderer.razor` | SVG bezier curves for block connections |
| CREATE | `src/web-app/WebApp/wwwroot/js/canvas-interop.js` | JS: pan/zoom, drag, HammerJS touch |

**MDI Layout Architecture**

```
CanvasHost
└── WindowContainer (position: relative; overflow: hidden)
    └── DocumentWindow[] (position: absolute; z-index driven by WindowManager)
        ├── TitleBar (drag handle, minimize, maximize, close, detach)
        ├── ResizeHandles (8 directional)
        └── ContentSlot (renders any panel component as child)
```

**Designer Canvas Architecture**

```
DesignerCanvas
├── SVG overlay (connections, drag ghost)
├── Block nodes (absolutely positioned div per block)
│   ├── Socket indicators (input left / output right)
│   └── Block body (type badge, parameter summary)
└── Block palette (FluentDrawer, slides in from left)
```

**Skills to Apply**
- `.skills/premium-uiux-blazor.md`
- `.skills/web-apps.md`
- `.skills/designer.md`

**Acceptance Criteria**
- [ ] `DocumentWindow` can be dragged, resized, minimized, maximized, closed
- [ ] Window positions/sizes persist to localStorage and restore on reload
- [ ] `DesignerCanvas` can add blocks by dragging from palette
- [ ] Socket connections drawn as smooth bezier curves with type-color coding
- [ ] Pan (middle-mouse/touch) and zoom (wheel/pinch) work correctly
- [ ] Touch gestures work on Android Chrome (HammerJS via JS interop)

**Session Status: ✅ COMPLETE**

---

### Session 12 — Trading + Arbitrage + DeFi Panels

**Objective**: Implement all domain-specific MDI panels: TradingTerminal, ArbitrageScanner, PathVisualization, DeFiPositions.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/Trading/TradingTerminal.razor` | Chart + order book + positions |
| CREATE | `src/web-app/WebApp/Components/Trading/TradingChart.razor` | ApexCharts candlestick + signal overlays |
| CREATE | `src/web-app/WebApp/Components/Trading/OrderBook.razor` | Real-time L2 depth display |
| CREATE | `src/web-app/WebApp/Components/Trading/PositionsGrid.razor` | Open positions with live P&L |
| CREATE | `src/web-app/WebApp/Components/Arbitrage/ArbitrageScanner.razor` | Live opportunity table |
| CREATE | `src/web-app/WebApp/Components/Arbitrage/PathVisualization.razor` | Cytoscape.js token graph |
| CREATE | `src/web-app/WebApp/Components/DeFi/DeFiPositions.razor` | Supply/borrow positions |
| CREATE | `src/web-app/WebApp/Components/DeFi/HealthMonitor.razor` | Health factor gauge + alert |

**Real-Time Update Pattern (all panels)**

```csharp
// No full component re-renders on data updates
// Use SignalR → JS interop → targeted DOM update
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;
    _subscription = await _hub.GetEnvelopeStreamAsync(["TRADE_SIGNAL", "POSITION_UPDATE"], ct)
        .ForEachAsync(envelope => InvokeAsync(() =>
        {
            // Update only changed data fields — no StateHasChanged for full render
            _positions[envelope.Symbol] = envelope.Payload;
            await JS.InvokeVoidAsync("updatePositionRow", envelope.Symbol, envelope.Payload);
        }), ct);
}
```

**Skills to Apply**
- `.skills/premium-uiux-blazor.md`
- `.skills/web-apps.md`
- `.skills/web3.md`

**Acceptance Criteria**
- [ ] `TradingChart` updates in real-time via SignalR without full re-render
- [ ] `ArbitrageScanner` shows live nHOP paths with profit/gas breakdown
- [ ] `PathVisualization` renders Cytoscape graph with token nodes and edge weights
- [ ] `HealthMonitor` triggers animated alert when health factor < 1.2
- [ ] All panels open as `DocumentWindow` instances from the CanvasHost

**Session Status: ✅ COMPLETE**

---

### Session 13 — ML Runtime Panel + Observatory + Envelope Viewer

**Objective**: Implement ML Runtime dashboard, Module Observatory, and diagnostic Envelope Viewer panels.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/MLRuntime/ModelRegistry.razor` | ✅ All models, versions, metrics |
| CREATE | `src/web-app/WebApp/Components/MLRuntime/InferenceMetrics.razor` | ✅ Latency histogram, throughput |
| CREATE | `src/web-app/WebApp/Components/MLRuntime/TrainProgress.razor` | ✅ Live training: loss curve, confusion matrix |
| CREATE | `src/web-app/WebApp/Components/Observatory/NetworkTopology.razor` | ✅ Cytoscape.js module graph |
| CREATE | `src/web-app/WebApp/Components/Observatory/ModuleCard.razor` | ✅ Health card per module |
| CREATE | `src/web-app/WebApp/Components/Observatory/EnvelopeStream.razor` | ✅ Live filtered envelope viewer |

**TrainProgress Live Chart Pattern**

```
TRAINING_JOB_PROGRESS envelope received
  → update loss_series[] and accuracy_series[]
  → JS.InvokeVoidAsync("updateApexSeries", "loss-chart", epoch, train_loss, val_loss)
  → JS.InvokeVoidAsync("updateApexSeries", "accuracy-chart", epoch, accuracy)
  
On TRAINING_JOB_COMPLETE:
  → Render confusion matrix using ApexCharts heatmap
  → Show SHAP feature importance bar chart
```

**Skills to Apply**
- `.skills/premium-uiux-blazor.md`
- `.skills/machine-learning.md`

**Acceptance Criteria**
- [x] `TrainProgress` renders live loss curve updating every epoch
- [x] `NetworkTopology` shows all registered modules as graph nodes with edge labels
- [x] `EnvelopeStream` filters by message type with regex search
- [x] `ModelRegistry` shows model lineage (parent training run → exported ONNX → deployed)

**Session Status: ✅ COMPLETE** — Implemented 2026-04-06

---

### Session 14 — AI Chat Panel + Data Feed Manager

**Objective**: Implement the AI Chat panel with streaming responses, canvas action rendering, and the Data Feed Manager.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/AI/AIChatPanel.razor` | ✅ Streaming chat with canvas action integration |
| CREATE | `src/web-app/WebApp/Components/AI/ProviderSettings.razor` | ✅ User-defined provider configuration |
| CREATE | `src/web-app/WebApp/Components/AI/CanvasActionRenderer.razor` | ✅ Render AI canvas actions as MDI panels |
| CREATE | `src/web-app/WebApp/Components/AI/MessageBubble.razor` | ✅ Chat message with code/diagram rendering |
| CREATE | `src/web-app/WebApp/Components/DataLayer/DataFeedManager.razor` | ✅ Active feeds, latency, gap status |
| CREATE | `src/web-app/WebApp/Services/AICanvasService.cs` | ✅ Receive AI_CANVAS_ACTION, open/update panels |

**AI Chat UX Flow**

```
User types: "Show BTC momentum strategy P&L for last 30 days"
  ↓
AIHub receives AI_QUERY (with ProjectSnapshot context)
  ↓
AnalyticsPlugin.PlotChart("BTCPERP", "1d") → AI_CANVAS_ACTION(OpenPanel, TradingChart)
TradingPlugin.GetPositions() → AI_CANVAS_ACTION(OpenPanel, PnLReport)
  ↓ (parallel to text response)
AI_CANVAS_ACTION received → AICanvasService.HandleAsync(action)
  → Opens TradingChart DocumentWindow in MDI canvas
  → Opens PnLReport DocumentWindow
  ↓
AI_RESPONSE_CHUNK[] → MessageBubble renders typed text with streaming effect
  ↓
Final message: "Here's the BTC chart [linked to opened panel] and the P&L summary..."
```

**Mermaid Diagram Rendering**

```razor
@* AI can emit ShowDiagramAction with Mermaid source *@
@* MessageBubble renders it inline using mermaid.js JS interop *@
<div @ref="_diagramRef" data-mermaid="@Message.MermaidSource" />
```

**Skills to Apply**
- `.skills/premium-uiux-blazor.md`
- `.skills/ai-hub.md`
- `.skills/web-apps.md`

**Acceptance Criteria**
- [x] Chat panel streams token-by-token with typing cursor effect
- [x] Canvas actions open real MDI panels before text response completes
- [x] Mermaid diagrams render correctly in chat messages
- [x] Provider settings persist across browser sessions (localStorage + PostgreSQL)
- [x] `DataFeedManager` shows per-feed latency, last candle time, gap count

**Session Status: ✅ COMPLETE** — Implemented 2026-04-06

---

## Phase 4 — Hydra Data Collection

### Session 15 — Exchange Feed Collectors + Gap Detection

**Objective**: Implement all exchange feed collection jobs in the Data Layer, gap detection engine, and backfill pipeline.

**Files Created**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/FeedCollector.cs` | ✅ Base feed collection loop + 100-candle/500ms write buffer |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/HyperliquidFeedCollector.cs` | ✅ WS feed from HYPERLIQUID — reconnect with exponential backoff |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/CamelotFeedCollector.cs` | ✅ Subgraph poll (1h→poolHourDatas, 1d→poolDayDatas; other TFs rejected) |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/FeedScheduler.cs` | ✅ ConcurrentDictionary-keyed start/stop/status per FeedKey |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/GapDetector.cs` | ✅ 60s PeriodicTimer; stale-feed gap detection via MAX(open_time) |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/BackfillPipeline.cs` | ✅ BoundedChannel<GapRange> (DropOldest); REST backfill (object + array form parsing) |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hydra/HydraUtils.cs` | ✅ SanitiseFeedId, SanitisePeerId, ParseJsonDouble/GetJsonDouble, TimeframeToSeconds |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Persistence/CandleEntity.cs` | ✅ OHLCV entity + BRIN index |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Persistence/CandleRepository.cs` | ✅ ON CONFLICT DO NOTHING batch upsert |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Persistence/DataLayerDbContext.cs` | ✅ EF Core 9 + Npgsql |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Hubs/DataLayerHub.cs` | ✅ SignalR hub — broadcast + per-peer groups (sanitised IDs) |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Controllers/FeedController.cs` | ✅ GET/POST/DELETE /api/feeds |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Services/BlockControllerClient.cs` | ✅ MODULE_REGISTER + 5s heartbeat |
| CREATE | `src/modules/data-layer/MLS.DataLayer/Program.cs` | ✅ HTTP 5700 + WS 6700 dual-port Kestrel binding; named HttpClients |
| CREATE | `src/modules/data-layer/MLS.DataLayer.Tests/` | ✅ 55 passing xUnit tests (HydraUtils, GapDetector, BackfillPipeline parse, CamelotTimeframes) |

**Gap Detection Algorithm (implemented)**

```
GapDetector (runs every 60s via PeriodicTimer):
  For each (exchange, symbol, timeframe) in active_feeds:
    latestStored = SELECT MAX(open_time) FROM candles WHERE ...
    elapsed = now - latestStored
    missingCount = floor(elapsed.TotalSeconds / tfSeconds) - 1
    if missingCount > 0:
      emit DATA_GAP_DETECTED { exchange, symbol, timeframe, gap_start, gap_end }
      BackfillPipeline.EnqueueAsync(gap)
```

> **Note**: The original `COUNT(*) WHERE open_time > latestStored` query was removed because it always returned 0 (nothing can exceed the MAX). The stale-feed formula above is accurate and simpler.

**Acceptance Criteria**
- [x] `HyperliquidFeedCollector` ingests real-time candles via WSS and persists to PostgreSQL with buffered batch writes (100 candles / 500ms flush)
- [x] `GapDetector` detects stale feeds by comparing elapsed time to timeframe interval; triggers backfill when `missingCount > 0`
- [x] `BackfillPipeline` fills detected gaps via HYPERLIQUID REST API (both object and positional-array response forms handled); emits `DATA_GAP_FILLED` on completion
- [x] All feed jobs recoverable after connection drop — exponential backoff 1s→60s with jitter, partial buffer flushed on disconnect
- [x] CamelotFeedCollector: unsupported timeframes (anything except `1h`/`1d`) rejected at scheduler entry point with a warning log
- [x] All user-controlled identifiers sanitised via `HydraUtils.SanitiseFeedId` / `SanitisePeerId` before logging or external queries
- [x] `BackfillPipeline` and `CamelotFeedCollector` use `IHttpClientFactory` named clients — no typed-client/singleton DI conflict
- [x] 55 xUnit tests passing (HydraUtils parse helpers, gap formula, Hyperliquid object+array parsing, Camelot timeframe set)

**Session Status: ✅ COMPLETE** — Implemented 2026-04-07

---

### Session 16 — Feature Store + FeatureEngineer Service

**Objective**: Implement the Feature Store schema and the FeatureEngineer service that computes all model input features from raw OHLCV data.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/data-layer/MLS.DataLayer/FeatureStore/FeatureStoreRepository.cs` | EF Core: feature_store table |
| CREATE | `src/modules/data-layer/MLS.DataLayer/FeatureStore/FeatureEngineer.cs` | Compute RSI/MACD/BB/ATR/VWAP from OHLCV |
| CREATE | `src/modules/data-layer/MLS.DataLayer/FeatureStore/FeatureSchema.cs` | Typed schema per model type |
| MODIFY | `src/modules/data-layer/docs/SESSION.md` | Add feature store section |

**Feature Computation Performance**

```csharp
// L1 vectorised using System.Numerics.Vector<float>
// All features computed in one pass over OHLCV window
// No Python dependency — pure C# for production inference path
public sealed class FeatureEngineer
{
    public FeatureVector ComputeModelT(ReadOnlySpan<OHLCVCandle> window)
    {
        // 8 features: RSI(14), MACD signal, BB position, VolumeΔ, 
        //             Momentum(20), ATR(14), SpreadBps, VWAPDistance
        // Target: < 1ms for 200-candle window
    }
}
```

**Acceptance Criteria**
- [ ] `FeatureEngineer.ComputeModelT` matches known Python reference values to 6 decimal places
- [ ] Feature computation for 200-candle window < 1ms (BenchmarkDotNet)
- [ ] Feature vectors persisted with versioned schema in `feature_store` table

---

## Phase 5 — PWA + Chrome Extension

### Session 17 — PWA Manifest + Service Worker + Responsive Breakpoints

**Objective**: Transform the Blazor web app into a PWA with offline capability and full mobile-responsive layout.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/wwwroot/manifest.json` | PWA manifest: name, icons, display:standalone |
| CREATE | `src/web-app/WebApp/wwwroot/service-worker.js` | Workbox: cache Blazor framework, network-first API |
| CREATE | `src/web-app/WebApp/wwwroot/service-worker-assets.js` | Asset manifest for offline cache |
| CREATE | `src/web-app/WebApp/wwwroot/icons/` | 192×192, 512×512, maskable icon variants |
| CREATE | `src/web-app/WebApp/wwwroot/css/responsive.css` | Container queries + breakpoint overrides |
| MODIFY | `src/web-app/WebApp/Components/Canvas/CanvasHost.razor` | Mobile: single panel stack mode |
| MODIFY | `src/web-app/WebApp/Components/Shared/MainLayout.razor` | FluentNavMenu responsive: hamburger on mobile |

**Responsive Breakpoint System**

```
> 1440px   Full MDI: floating windows, multi-panel, full palette
1024-1440  Docked: split panels, collapsible sidebar, no floating
768-1024   Tablet: single active panel + bottom sheet navigation
< 768px    Mobile: single panel stack, FAB for panel selection
```

**Service Worker Cache Strategy**

```javascript
// Blazor framework files: CacheFirst (immutable, versioned)
// API calls: NetworkFirst (trading data must be fresh)
// Shell HTML: StaleWhileRevalidate
// Icons + fonts: CacheFirst with 30-day expiry
// Background sync: queue orders when offline, replay on reconnect
```

**Skills to Apply**
- `.skills/pwa-chrome.md` ← new skill
- `.skills/premium-uiux-blazor.md`
- `.skills/web-apps.md`

**Acceptance Criteria**
- [ ] Lighthouse PWA score ≥ 90
- [ ] App installable on Android Chrome and iOS Safari
- [ ] Offline: designer canvas works without network (WASM mode)
- [ ] Mobile: all panels accessible via bottom sheet, no horizontal overflow

---

### Session 18 — Chrome Extension (MV3)

**Objective**: Build a Chrome extension that embeds the MLS platform in Chrome's Side Panel API and injects price overlays on exchange websites.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/wwwroot/chrome-extension/manifest.v3.json` | Extension manifest (MV3) |
| CREATE | `src/web-app/WebApp/wwwroot/chrome-extension/background.js` | Service worker: connect to MLS WS |
| CREATE | `src/web-app/WebApp/wwwroot/chrome-extension/content.js` | Inject price overlay on exchange sites |
| CREATE | `src/web-app/WebApp/wwwroot/chrome-extension/sidebar/` | Side Panel: mini MDI with key panels |
| CREATE | `src/web-app/WebApp/wwwroot/chrome-extension/popup/` | Toolbar popup: quick stats + chat |
| CREATE | `src/web-app/WebApp/wwwroot/chrome-extension/icons/` | 16, 32, 48, 128px icons |

**Extension Side Panel**

```
Chrome Side Panel (persistent, always visible while browsing)
├── Mini TradingChart (BTCPERP by default, symbol picker)
├── Open Positions summary
├── ArbitrageScanner (top 3 opportunities)
└── AI Chat (full chat with mini canvas actions)

All data via chrome.runtime.connect → background.js → MLS WebSocket
```

**Acceptance Criteria**
- [ ] Extension installs from `chrome://extensions` developer mode
- [ ] Side Panel opens on MLS-connected tab and shows live BTC chart
- [ ] Background service worker reconnects after Chrome restart
- [ ] Price overlay appears on Hyperliquid and Camelot web interfaces

---

## Phase 6 — Dynamic Compilation

### Session 19 — Roslyn Strategy Compiler + Dynamic Block Loader

**Objective**: Enable live C# code editing in the designer with Roslyn compilation and IPFS-based strategy distribution.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/designer/MLS.Designer/Compilation/IStrategyCompiler.cs` | Interface |
| CREATE | `src/modules/designer/MLS.Designer/Compilation/RoslynStrategyCompiler.cs` | Roslyn: compile custom block to assembly |
| CREATE | `src/modules/designer/MLS.Designer/Compilation/DynamicBlockLoader.cs` | Load compiled block from IPFS CID |
| CREATE | `src/modules/designer/MLS.Designer/Compilation/CompilationSandbox.cs` | Restricted AppDomain for user code |
| MODIFY | `src/web-app/WebApp/Components/Designer/PropertyEditor.razor` | Add code editor (Monaco JS interop) |

**Security Model**

```
User code runs in CompilationSandbox:
  - No access to filesystem
  - No network access (only via IBlockSocket outputs)
  - No reflection on MLS internals
  - 100ms execution timeout per ProcessAsync call
  - Memory limit: 64MB per sandbox
  - Disposable on strategy stop
```

**Acceptance Criteria**
- [x] Custom C# indicator block compiles and runs in < 2s from save
- [x] Sandbox prevents file system access (test: attempt File.ReadAllText)
- [x] Compiled block IPFS CID stored in strategy schema for distribution
- [x] Monaco editor in browser with C# syntax highlighting and IntelliSense

**Completed**: Session 19 fully implemented.
- `IStrategyCompiler` interface + `CompilationResult` record
- `RoslynStrategyCompiler`: Roslyn 4.8.0 compilation, forbidden-namespace source scan, IPFS upload
- `CompilationSandbox`: collectible `AssemblyLoadContext`, 100 ms timeout, 64 MB memory limit, `IAsyncDisposable`
- `DynamicBlockLoader`: download assembly from IPFS CID → load into sandbox
- `DesignerOptions.IpfsApiUrl` + IPFS named `HttpClient` registered in `Program.cs`
- `StrategySchema.CompiledBlockCid` column + `StrategyRepository.SetCompiledBlockCidAsync`
- `CompilationController`: `POST /api/compile`, `POST /api/compile/upload`
- `BlockSocket` made `public` to enable user code extension of `BlockBase`
- `PropertyEditor.razor`: Monaco editor (CDN), JS interop (`monaco-interop.js`), `CompileAsync` round-trip, CID display
- 14 new xUnit tests in `CompilationSandboxTests.cs` (80 total, all passing)

---

### Session 20 — HyperparamSearch Block + Optuna Integration

**Objective**: Implement the HyperparamSearchBlock using Bayesian optimization (Optuna in Shell VM) with live progress streaming.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/modules/designer/MLS.Designer/Blocks/MLTraining/HyperparamSearchBlock.cs` | Orchestrate Optuna search |
| CREATE | `src/modules/ml-runtime/scripts/hyperparam_search.py` | Optuna study + training loop |
| MODIFY | `src/web-app/WebApp/Components/MLRuntime/TrainProgress.razor` | Add hyperparameter search view |

**Optuna Search Flow**

```
HyperparamSearchBlock config:
  - search_space: { lr: [1e-4, 1e-2], dropout: [0.1, 0.5], hidden_dims: [[64,32], [128,64,32]] }
  - n_trials: 50
  - direction: maximize(sharpe)
  - sampler: TPESampler (Bayesian)
  - pruner: HyperbandPruner

Each trial:
  1. Emit TRAINING_JOB_START with trial hyperparams
  2. Receive TRAINING_JOB_PROGRESS → report to Optuna trial
  3. Pruner may stop trial early if intermediate values are worse
  4. On study complete: emit best_params + best_value in TRAINING_JOB_COMPLETE
```

**Acceptance Criteria**
- [ ] `hyperparam_search.py` runs 5 trial mini-study to completion in < 60s
- [ ] Pruner correctly stops underperforming trials early
- [ ] Best hyperparameters persisted in model registry
- [ ] Live trial progress shown in `TrainProgress` panel

---

## Phase 7 — Visualization + Benchmarks

### Session 21 — Live Canvas Envelope Pulse Animations

**Objective**: Animate the designer canvas to show real-time envelope flow between blocks — each live message produces a visible pulse on the connecting wire.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| MODIFY | `src/web-app/WebApp/Components/Designer/ConnectionRenderer.razor` | Animate pulse on BLOCK_SIGNAL |
| CREATE | `src/web-app/WebApp/wwwroot/js/canvas-pulse.js` | SVG SMIL animation: pulse along bezier path |
| MODIFY | `src/web-app/WebApp/Components/Designer/DesignerCanvas.razor` | Overlay: block latency badge |
| CREATE | `src/web-app/WebApp/Components/Designer/LiveOverlay.razor` | Real-time: inferences/s, fill rate, P&L delta |

**Animation Spec**

```css
/* Pulse: animated circle travels along SVG path */
/* Color = socket type: cyan=candle, amber=indicator, green=signal, red=risk-block */
/* Duration: 300ms, triggered on each envelope routed through that connection */
/* Wrap in @media (prefers-reduced-motion: no-preference) */
```

**Block Status Badges**

```
Each block node in the canvas shows:
  ├── Inference latency (ms) — live, last 100ms rolling avg
  ├── Messages/sec through block
  ├── Error rate (last 60s)
  └── Status dot: green=active, amber=degraded, red=error
```

**Acceptance Criteria**
- [ ] Pulse animation fires within 50ms of envelope routing through connection
- [ ] Animation uses only `transform` + `opacity` (no layout reflow)
- [ ] Block latency badge updates at 1Hz without triggering full Blazor render
- [ ] `@media (prefers-reduced-motion)` disables all animations correctly

---

### Session 22 — BenchmarkDotNet Suite + Performance Baselines

**Objective**: Create comprehensive BenchmarkDotNet suite covering all critical hot paths with performance baselines documented.

**Files to Create**

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/benchmarks/MLS.Benchmarks.csproj` | Benchmark project |
| CREATE | `src/benchmarks/EnvelopeRoutingBench.cs` | Envelope parsing + topic routing |
| CREATE | `src/benchmarks/IndicatorBlockBench.cs` | RSI/MACD/BB/ATR vectorised computation |
| CREATE | `src/benchmarks/ONNXInferenceBench.cs` | model-t inference with pre-allocated tensors |
| CREATE | `src/benchmarks/FeatureEngineerBench.cs` | Feature vector computation from OHLCV |
| CREATE | `src/benchmarks/StrategyRouterBench.cs` | Strategy deploy + subscription lookup |
| CREATE | `docs/architecture/performance-baselines.md` | Recorded baseline results |

**Benchmark Targets**

| Benchmark | Target | Measurement |
|-----------|--------|-------------|
| Envelope parse + route | < 1µs | Median p50 |
| RSI(14) single candle | < 100ns | Median p50 |
| MACD full compute | < 500ns | Median p50 |
| Feature vector (8 features) | < 1ms/200 candles | Median p50 |
| model-t ONNX inference | < 10ms | p95 |
| Subscription lookup | < 200ns | Median p50 |
| Strategy deploy (100 blocks) | < 5ms | Median p50 |

**Acceptance Criteria**
- [ ] All benchmarks run with `dotnet run -c Release` (no external dependencies)
- [ ] All targets met or documented with reason if missed
- [ ] Memory allocations reported: 0 alloc on envelope routing hot path
- [ ] Results saved to `docs/architecture/performance-baselines.md`

---

## Cross-Session References

### Skills Reference Index

| Skill File | Used In Sessions |
|------------|-----------------|
| `.skills/designer.md` | 01, 04, 05, 06, 07, 11 |
| `.skills/ai-hub.md` | 08, 09, 10, 14 |
| `.skills/pwa-chrome.md` | 17, 18 |
| `.skills/exchange-adapters.md` | 05, 15 |
| `.skills/hydra-collector.md` | 07, 15, 16 |
| `.skills/beast-development.md` | 01, 02, 04, 22 |
| `.skills/machine-learning.md` | 04, 06, 13, 16 |
| `.skills/acceleration/acceleration.md` | 06, 16, 22 |
| `.skills/models/model-t.md` | 04, 06, 09 |
| `.skills/models/model-a.md` | 05, 06 |
| `.skills/models/model-d.md` | 05, 06 |
| `.skills/premium-uiux-blazor.md` | 11, 12, 13, 14, 17 |
| `.skills/web3.md` | 05, 12 |
| `.skills/semantic-kernel.md` | 08, 09, 10 |
| `.skills/agents.md` | 08, 09, 10 |
| `.skills/dotnet-devs.md` | 01–22 (always) |
| `.skills/websockets-inferences.md` | 02, 04, 08 |
| `.skills/storage-data-management.md` | 07, 16 |

### Envelope Types Quick Reference

| Envelope Type | Producer | Consumer | Sessions |
|--------------|----------|----------|---------|
| `STRATEGY_DEPLOY` | designer, web-app | block-controller | 01, 02 |
| `STRATEGY_STATE_CHANGE` | block-controller | all | 02, 07 |
| `BLOCK_SIGNAL` | any block | next block | 01, 04 |
| `TRAINING_JOB_START` | designer (TrainModelBlock) | shell-vm | 06 |
| `TRAINING_JOB_PROGRESS` | shell-vm | designer, web-app | 06, 13 |
| `TRAINING_JOB_COMPLETE` | shell-vm | designer, ml-runtime | 06, 13 |
| `AI_QUERY` | web-app | ai-hub | 10, 14 |
| `AI_RESPONSE_CHUNK` | ai-hub | web-app | 10, 14 |
| `AI_CANVAS_ACTION` | ai-hub | web-app | 09, 10, 14 |
| `DATA_COLLECTION_START` | designer | data-layer | 07, 15 |
| `DATA_GAP_DETECTED` | data-layer | designer, web-app | 15 |
| `DATA_GAP_FILLED` | data-layer | designer, web-app | 15 |
| `EXCHANGE_PRICE_UPDATE` | exchange adapters | arbitrager | 05, 15 |
| `ARB_PATH_FOUND` | arbitrager | designer, broker | 05, 12 |
| `DEFI_HEALTH_WARNING` | defi | designer, ai-hub | 05, 12 |
| `CANVAS_LAYOUT_SAVE` | web-app | block-controller | 02, 11 |

### Module Port Reference (Complete)

| Module | HTTP | WS | New? |
|--------|------|----|------|
| block-controller | 5100 | 6100 | existing |
| web-app | 5200 | 6200 | existing |
| **designer** | **5250** | **6250** | **NEW (Phase 1)** |
| trader | 5300 | 6300 | existing |
| arbitrager | 5400 | 6400 | existing |
| defi | 5500 | 6500 | existing |
| ml-runtime | 5600 | 6600 | existing |
| data-layer | 5700 | 6700 | existing |
| **ai-hub** | **5750** | **6750** | **NEW (Phase 2)** |
| broker | 5800 | 6800 | existing |
| transactions | 5900 | 6900 | existing |
| shell-vm | 5950 | 6950 | existing |
