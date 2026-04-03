# Designer Module — Session Prompt

> Use this document as context when generating Designer module code with GitHub Copilot.
> Cross-reference: [Designer Block Graph](../../docs/architecture/designer-block-graph.md) | [Session Schedule — Phase 1](../../docs/session-schedule.md#phase-1--designer-module)

---

## 1. Module Identity

| Field | Value |
|---|---|
| **Name** | `designer` |
| **Namespace** | `MLS.Designer` |
| **Role** | Block graph composer for trading strategies, arbitrage paths, DeFi moves, ML training pipelines, and data hydra connections |
| **HTTP Port** | `5250` |
| **WebSocket Port** | `6250` |
| **Container** | `mls-designer` |
| **Docker image** | `ghcr.io/somat3k/mls-designer:latest` |

---

## 2. Technology Stack

| Technology | Purpose |
|---|---|
| .NET 9 ASP.NET Core | Module host, REST API |
| SignalR | Real-time canvas updates to web-app |
| Microsoft.ML.OnnxRuntime | ONNX inference in MLBlocks |
| Microsoft.CodeAnalysis (Roslyn) | CustomIndicatorBlock live compilation (Phase 6) |
| System.Numerics.Vector<T> | Vectorised indicator computations |
| Npgsql + EF Core 9 | Strategy persistence, feature store queries |
| MessagePack-CSharp | Envelope wire serialization |

---

## 3. Block Domain Structure

```
MLS.Designer/
├── Blocks/
│   ├── Trading/          ← DataSource, Indicator, ML, Strategy, Risk, Execution blocks
│   ├── Arbitrage/        ← SpreadCalculator, nHOPFinder, FlashLoan, ProfitGate
│   ├── DeFi/             ← Morpho, Balancer, CollateralHealth, YieldOptimizer
│   ├── MLTraining/       ← DataLoader, FeatureEngineer, TrainModel, ExportONNX, HyperSearch
│   └── DataHydra/        ← FeedSource, Filter, Normalise, Router, BackfillBlock, GapMonitor
│
├── Exchanges/
│   ├── IExchangeAdapter.cs
│   ├── HyperliquidAdapter.cs
│   ├── CamelotAdapter.cs
│   ├── DFYNAdapter.cs
│   ├── BalancerAdapter.cs
│   ├── MorphoAdapter.cs
│   └── ExchangeRegistry.cs
│
├── Composition/
│   ├── ICompositionGraph.cs
│   ├── CompositionGraph.cs
│   └── UndoManager.cs
│
├── Execution/
│   ├── StrategyRunner.cs      ← live execution
│   ├── BacktestEngine.cs      ← historical replay
│   └── PaperEngine.cs         ← paper trading (parallel to live)
│
├── Compilation/               ← Phase 6
│   ├── IStrategyCompiler.cs
│   ├── RoslynStrategyCompiler.cs
│   ├── DynamicBlockLoader.cs
│   └── CompilationSandbox.cs
│
├── Persistence/
│   ├── StrategyRepository.cs
│   └── StrategySchema.cs
│
├── Controllers/
│   ├── BlocksController.cs    GET /api/blocks, GET /api/blocks/{type}
│   └── StrategiesController.cs  Full CRUD + deploy/stop/backtest
│
└── Hubs/
    └── DesignerHub.cs
```

---

## 4. Key Interfaces

```csharp
// Single processing node (StockSharp BaseIndicator equivalent)
public interface IBlockElement
{
    Guid BlockId { get; }
    string BlockType { get; }
    IReadOnlyList<IBlockSocket> InputSockets { get; }
    IReadOnlyList<IBlockSocket> OutputSockets { get; }
    IReadOnlyList<BlockParameter> Parameters { get; }
    ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct);
    Task PreloadAsync(IEnumerable<BlockSignal> historicalData, CancellationToken ct);
    void Reset();
}

// Nestable strategy composition (StockSharp CompositionDiagramElement equivalent)
public interface ICompositionGraph
{
    Guid GraphId { get; }
    string Name { get; }
    int SchemaVersion { get; }   // Increment on every structural change
    IReadOnlyList<IBlockElement> Blocks { get; }
    IReadOnlyList<BlockConnection> Connections { get; }
    IReadOnlyList<IBlockSocket> GetExposedPorts();
    Task ConnectAsync(Guid fromSocketId, Guid toSocketId, CancellationToken ct);
    Task DeployAsync(CancellationToken ct);
}

// Exchange adapter (StockSharp IMessageAdapter equivalent)
public interface IExchangeAdapter : IAsyncDisposable
{
    string ExchangeId { get; }
    bool IsConnected { get; }
    Task<decimal> GetPriceAsync(string symbol, CancellationToken ct);
    IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(string symbol, CancellationToken ct);
    Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct);
}
```

---

## 5. Envelope Types Produced

| Envelope | When |
|---|---|
| `STRATEGY_DEPLOY` | User deploys strategy from canvas |
| `STRATEGY_STATE_CHANGE` | Strategy started/stopped/backtesting |
| `BLOCK_SIGNAL` | Block emits output to connected block |
| `TRAINING_JOB_START` | TrainModelBlock dispatches to Shell VM |
| `DATA_COLLECTION_START` | FeedSourceBlock starts Hydra feed |

## 6. Envelope Types Consumed

| Envelope | Action |
|---|---|
| `CANDLE_STREAM` | Feed to DataSourceBlocks |
| `INFERENCE_RESULT` | Feed to MLInferenceBlocks |
| `TRAINING_JOB_PROGRESS` | Update TrainProgress panel |
| `TRAINING_JOB_COMPLETE` | Trigger ExportONNXBlock |
| `DATA_GAP_DETECTED` | Alert in DataFeedManager panel |
| `ARB_PATH_FOUND` | Update PathVisualization panel |
| `DEFI_HEALTH_WARNING` | Alert in DeFiPositions panel |

---

## 7. Block Registration

```csharp
// All block types registered at startup
services.AddSingleton<IBlockRegistry, BlockRegistry>();

// Registration pattern:
services.AddKeyedScoped<IBlockElement, RSIBlock>("RSIBlock");
services.AddKeyedScoped<IBlockElement, MACDBlock>("MACDBlock");
services.AddKeyedScoped<IBlockElement, ModelTInferenceBlock>("ModelTInferenceBlock");
// ... all blocks registered
```

---

## 8. nHOP Arbitrage Path Finder

```
Tokens: [WETH, USDC, ARB, WBTC, GMX, RDNT]
Exchanges: [Camelot, DFYN, Balancer, Hyperliquid]
Max hops: 4
Min profit (after gas): $10 USD

Algorithm: BFS on directed token-exchange graph
  → BellmanFord negative cycle detection for circular arbitrage
  → Rank by net_profit / required_capital
  → Top 3 paths emitted as ARB_PATH_FOUND envelopes
```

---

## 9. Configuration

```json
{
  "MLS": {
    "Module": "designer",
    "HttpPort": 5250,
    "WebSocketPort": 6250,
    "Network": {
      "BlockControllerUrl": "http://block-controller:5100",
      "BlockControllerWsUrl": "ws://block-controller:6100/ws/hub",
      "MLRuntimeUrl": "http://ml-runtime:5600",
      "DataLayerUrl": "http://data-layer:5700",
      "ShellVMWsUrl": "ws://shell-vm:6950/ws/hub"
    },
    "Designer": {
      "MaxBlocksPerStrategy": 200,
      "MaxConnectionsPerBlock": 8,
      "TemplatesPath": "/app/designer-templates",
      "BacktestMaxDays": 365
    }
  }
}
```

---

## 10. Skills to Apply

- `.skills/designer.md` — block graph patterns, socket types, composition rules
- `.skills/machine-learning.md` — ONNX inference in MLBlocks
- `.skills/models/model-t.md` — model-t feature schema
- `.skills/models/model-a.md` — model-a feature schema
- `.skills/models/model-d.md` — model-d feature schema
- `.skills/web3.md` — exchange adapters, Nethereum, blockchain addresses
- `.skills/exchange-adapters.md` — IExchangeAdapter, nHOP, Arbitrum DEX specifics
- `.skills/hydra-collector.md` — data feed blocks, gap detection
- `.skills/beast-development.md` — computation circuits, indicator vectorisation
- `.skills/acceleration/acceleration.md` — L1–L4 acceleration
- `.skills/dotnet-devs.md` — C# 13, DI, async, nullable
- `.skills/storage-data-management.md` — EF Core, feature store
