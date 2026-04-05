# 🗂️ MLS Platform — Extensive Development Checklist

> Track development progress, testing, debugging, and deployment readiness across all modules.

## 📦 Repository Setup

### Infrastructure & Configuration
- [ ] Clone repository and configure `.env` from `.env.example`
- [ ] Start infrastructure: `docker compose -f docker-compose.infra.yml up -d`
- [ ] Verify PostgreSQL connection: `docker exec mls-postgres pg_isready`
- [ ] Verify Redis connection: `docker exec mls-redis redis-cli ping`
- [ ] Verify IPFS node: `docker exec mls-ipfs ipfs id`
- [ ] Run database init scripts: `infra/postgres/init/01-init.sql`
- [ ] Install .NET 9 SDK, Python 3.12, Node.js 20
- [ ] Install VS Code extensions from `.vscode/extensions.json`
- [ ] Run `dotnet restore MLS.sln`

---

## 🔵 Block Controller (Root Orchestrator)

### Development
- [ ] Create solution: `src/block-controller/BlockController.csproj` (net9.0)
- [ ] Implement `IModuleRegistry` — register/deregister modules
- [ ] Implement `IHeartbeatMonitor` — receive/timeout module heartbeats
- [ ] Implement `IMessageRouter` — route envelopes between modules
- [ ] Implement `ISubscriptionManager` — topic-based pub/sub
- [ ] Implement WebSocket server on port 6100
- [ ] Implement HTTP API on port 5100
- [ ] Endpoints: `POST /api/modules/register`, `DELETE /api/modules/{id}`, `GET /api/modules`, `GET /health`
- [ ] Implement `EnvelopePayload` record with all required fields
- [ ] Implement all `MessageTypes` constants
- [ ] Implement heartbeat timeout detection (deregister after 3 missed)

### Testing
- [ ] Unit test: `ModuleRegistry` — register, deregister, query
- [ ] Unit test: `HeartbeatMonitor` — detect timeouts
- [ ] Unit test: `MessageRouter` — routing logic
- [ ] Integration test: Full module registration flow via WebSocket
- [ ] Integration test: Heartbeat → timeout → deregistration cycle
- [ ] Integration test: Message routing between two modules

### Debug Checklist
- [ ] Block Controller starts and listens on ports 5100/6100
- [ ] `/health` endpoint returns 200
- [ ] WebSocket connections accepted on `ws://localhost:6100`
- [ ] Registration envelope received and processed
- [ ] Module appears in registry after registration
- [ ] Heartbeat updates `last_heartbeat` in PostgreSQL

---

## 🌐 Web Application (Blazor MDI)

### Development
- [ ] Create Blazor project: `src/web-app/WebApp.csproj` (net9.0)
- [ ] Add `Microsoft.FluentUI.AspNetCore.Components` NuGet package
- [ ] Add `Microsoft.FluentUI.AspNetCore.Components.Icons` NuGet package
- [ ] Configure `builder.Services.AddFluentUIComponents()` in Program.cs
- [ ] Implement MDI Canvas layout component
- [ ] Implement main navigation with FluentNavMenu
- [ ] Implement Dashboard page with multi-module overview
- [ ] Implement Trader page with chart and order panel
- [ ] Implement Arbitrager page with opportunity scanner
- [ ] Implement DeFi page with on-chain operations panel
- [ ] Implement Network topology page
- [ ] Implement Observatory page (metrics, charts, manifests)
- [ ] Implement Settings page
- [ ] Add SignalR client for real-time data
- [ ] Implement MDI panel persistence (localStorage via JS interop)
- [ ] Add candlestick chart component
- [ ] Add order book component
- [ ] Add portfolio P&L component

### Testing
- [ ] Unit test: MDI canvas panel state management
- [ ] Unit test: Chart data transformation
- [ ] Unit test: SignalR message deserialization
- [ ] Integration test: WebSocket connection to Block Controller
- [ ] UI test: Navigation between pages
- [ ] UI test: MDI panel open/close/resize

### Debug Checklist
- [ ] Web app starts on https://localhost:7200
- [ ] FluentUI providers loaded (check browser console for errors)
- [ ] No missing provider warnings
- [ ] SignalR connection established
- [ ] Real-time data updates visible in UI

---

## 🎨 Designer Module

### Session 06 — ML Training Domain Blocks + Training Pipeline
- [x] Implement `MLTraining/` block domain (DataLoader, FeatureEngineer, TrainSplit, TrainModel, ValidateModel, ExportONNX, HyperparamSearch)
- [x] Implement `TrainingDispatcher` service — dispatch `TRAINING_JOB_START` via Block Controller SignalR
- [x] Implement `training_pipeline.py` unified Python entry point (model-t/a/d, ONNX+JOBLIB export, IPFS upload)
- [x] `TrainModelBlock` emits `TRAINING_JOB_START` with correct feature schema per model type
- [x] Shell VM streams `TRAINING_JOB_PROGRESS` per epoch, `TRAINING_JOB_COMPLETE` on finish
- [x] `ExportONNXBlock` uploads to IPFS and records CID in PostgreSQL

### Session 07 — Universal Tile Builder + Transformation Controller + Hydra Data Domain
- [x] Implement `ICustomTile`, `ITileRule`, `ITileCondition`, `ITileAction` in MLS.Core.Designer
- [x] Implement `IActionTile` (Block-as-ONE autonomous data source pattern)
- [x] Implement `TransformationUnit`, `TransformationEnvelope`, `ITransformationController`, `LabelSchema`
- [x] Implement `CustomIndicatorTile`, `PassThroughTile` blocks + `TileRuleEngine` service
- [x] Implement `TransformationController` service + `LendingHealthBlock` as `IActionTile`
- [x] Implement `DataHydra/` blocks: FeedSource, Filter, Normalisation, Router, Backfill, GapMonitor
- [x] Implement `StrategyRepository` (EF Core CRUD), `DesignerDbContext`, `StrategiesController`
- [x] Add 6 strategy JSON templates to `designer-templates/`
- [x] Architecture docs: session-07-universal-tile-builder.md, session-07-label-schema.md, session-07-block-action-tiles.md, transformation-controller.md

---

## 🤖 AI Hub Module

### Session 08 — AI Hub Module Scaffold + Provider Router
- [x] Create `MLS.AIHub` project (net9.0, SK 1.74.0, EF Core 9, Npgsql)
- [x] Implement `ILLMProvider` interface (wraps SK `IChatCompletionService`)
- [x] Implement `OpenAIProvider` (GPT-4o, GPT-4-turbo, o3)
- [x] Implement `AnthropicProvider` (Claude 3.5 Sonnet, Claude 3 Opus — custom HTTP adapter)
- [x] Implement `GoogleProvider` (Gemini 2.5 Pro, Gemini Flash — SK Google connector)
- [x] Implement `GroqProvider` (Llama3-70b, Mixtral — OpenAI-compatible)
- [x] Implement `OpenRouterProvider` (100+ model routing — OpenAI-compatible)
- [x] Implement `VercelAIProvider` (AI SDK edge endpoint — OpenAI-compatible)
- [x] Implement `LocalProvider` (Ollama/llama.cpp — always-available final fallback)
- [x] All 7 providers share `ProviderBase` circuit breaker (3 failures → 60s open)
- [x] Implement `ProviderRouter` (user-defined distributor: override → prefs → fallback → local)
- [x] Implement `IUserPreferenceRepository` + `UserPreferenceRepository` (EF Core PostgreSQL)
- [x] Implement `BlockControllerClient` (MODULE_REGISTER + heartbeat every 5s)
- [x] Module starts on HTTP 5750 / WS 6750
- [x] Unit tests: `ProviderRouterTests` (5 tests including `SelectProvider_FallsBackToLocalWhenPrimaryUnavailable`)

---

### Session 09 — Semantic Kernel Plugins (All Domains)
- [x] Implement `TradingPlugin` (GetPositions, PlaceOrder, GetSignalHistory, GetPnLSummary)
- [x] Implement `DesignerPlugin` (CreateStrategy, AddBlock, RunBacktest, ExplainStrategy)
- [x] Implement `AnalyticsPlugin` (PlotChart, GenerateSHAP, ExportReport, AskAboutData)
- [x] Implement `MLRuntimePlugin` (TrainModel, GetModelMetrics, DeployModel, ListModels)
- [x] Implement `DeFiPlugin` (GetHealthFactors, SimulateRebalance, GetPoolAPYs, SupplyCollateral)
- [x] Implement `ContextAssembler` (8 parallel sources, < 200ms, per-source 120ms timeout)
- [x] Implement `ProjectSnapshot` with all typed module data DTOs
- [x] Implement `CanvasAction` discriminated union (OpenPanel, UpdateChart, HighlightBlock, ShowDiagram, AddAnnotation, OpenDesignerGraph)
- [x] Implement `CanvasActionDispatcher` (wraps `IHubContext<AIHub>`, sends `AI_CANVAS_ACTION` envelopes)
- [x] All state-modifying `[KernelFunction]` require `confirmed: bool` parameter
- [x] Integration tests: `PluginPipelineTests` (25+ tests covering all plugins and canvas actions)

---

### Session 10 — Canvas Action Dispatcher + AI Chat Streaming
- [x] Add `AI_RESPONSE_COMPLETE` to `MessageTypes.Designer.cs`
- [x] Create `AiResponseCompletePayload` core contract
- [x] Implement `IChatService` + `ChatService` (AI_QUERY → context → SK streaming → SignalR chunks)
- [x] Implement `AIHub` SignalR hub (`SendEnvelope` wired to `ChatService.ProcessQueryAsync`)
- [x] Implement `ChatController` (POST /api/chat → SignalR path, GET /api/chat/stream → SSE path)
- [x] `ChatService.ProcessQueryAsync` sends `AI_RESPONSE_CHUNK` per token + `AI_RESPONSE_COMPLETE`
- [x] `ChatService.StreamChunksAsync` returns `IAsyncEnumerable<AiResponseChunkPayload>` for SSE
- [x] SK `FunctionChoiceBehavior.Auto()` enables automatic plugin invocation during streaming
- [x] Canvas actions dispatched by plugins as side-effects (parallel with text streaming)
- [x] Integration tests: `ChatServiceTests` (8 tests — chunks, sequencing, provider routing, SSE terminal)

---



### Development
- [ ] Create project: `src/modules/trader/Trader.csproj`
- [ ] Implement `ITradingStrategy` interface
- [ ] Implement `ISignalGenerator` with ONNX inference
- [ ] Implement `IOrderManager` — create/cancel/modify orders
- [ ] Implement `IPositionTracker` — track open positions
- [ ] Implement `IRiskManager` — position sizing, stop loss
- [ ] Implement WebSocket server on port 6300
- [ ] Implement HTTP API on port 5300
- [ ] Register with Block Controller on startup
- [ ] Send heartbeat every 5 seconds
- [ ] Subscribe to market data from Data Layer
- [ ] Implement paper trading mode
- [ ] Load ONNX model for signal generation

### Testing
- [ ] Unit test: Signal generation with mock ONNX model
- [ ] Unit test: Risk manager position sizing calculations
- [ ] Unit test: Order manager state machine
- [ ] Integration test: End-to-end signal → order flow
- [ ] Integration test: Block Controller registration
- [ ] Benchmark: Signal inference < 10ms

### Debug Checklist
- [ ] Module starts and registers with Block Controller
- [ ] `/health` endpoint returns module status
- [ ] WebSocket accepts connections on port 6300
- [ ] Market data subscription active
- [ ] Trade signal generated and published to Block Controller

---

## 🔄 Arbitrager Module

### Development
- [ ] Create project: `src/modules/arbitrager/Arbitrager.csproj`
- [ ] Implement `IArbitrageDetector` — scan for price discrepancies
- [ ] Implement `IOpportunityScorer` with ML model
- [ ] Implement `IArrayBuilder` — construct arbitrage transaction arrays
- [ ] Subscribe to multiple exchange feeds simultaneously
- [ ] Implement spread calculation with fee consideration
- [ ] Implement execution via Transactions module
- [ ] Implement blockchain-only address validation
- [ ] WebSocket server on port 6400, HTTP on port 5400

### Testing
- [ ] Unit test: Spread calculation with fees
- [ ] Unit test: Opportunity scoring model
- [ ] Unit test: Array builder transaction construction
- [ ] Integration test: Multi-exchange feed subscription
- [ ] Integration test: Opportunity → transaction flow

---

## ⛓️ DeFi Module

### Development
- [ ] Create project: `src/modules/defi/DeFi.csproj`
- [ ] Implement `IWalletProvider` interface
- [ ] Implement HYPERLIQUID API client
- [ ] Implement HYPERLIQUID WebSocket client (order book, fills)
- [ ] Implement paper trading mode (parallel to live)
- [ ] Implement broker fallback logic (Broker1 → Broker2)
- [ ] Implement on-chain transaction broadcaster
- [ ] Load blockchain addresses from PostgreSQL only (never hardcoded)
- [ ] Implement `BlockchainAddress` enum mappings
- [ ] WebSocket server on port 6500, HTTP on port 5500
- [ ] **NEVER integrate Uniswap**

### Testing
- [ ] Unit test: HYPERLIQUID API client with mock server
- [ ] Unit test: Blockchain address resolver
- [ ] Unit test: Broker fallback logic
- [ ] Integration test: Paper trading order placement
- [ ] Integration test: WebSocket connection to HYPERLIQUID

---

## 🧠 ML Runtime Module

### Development (Python — Training)
- [ ] Set up Python environment: `src/modules/ml-runtime/`
- [ ] Create `pyproject.toml` with all dependencies
- [ ] Implement `train_trader.py` — train trader signal model
- [ ] Implement `train_arbitrager.py` — train arbitrage scorer
- [ ] Implement `train_defi.py` — train DeFi strategy model
- [ ] Implement ONNX export for all models
- [ ] Implement JOBLIB export for all models
- [ ] Implement model validation (accuracy, latency benchmarks)
- [ ] Configure Adam optimizer with correct hyperparameters
- [ ] Implement FastForest classification pipeline

### Development (C# — Inference)
- [ ] Create project with `Microsoft.ML.OnnxRuntime`
- [ ] Implement `IOnnxInferenceService` for model loading and inference
- [ ] Implement model hot-reload (replace model without restart)
- [ ] WebSocket streaming inference endpoint
- [ ] HTTP batch inference endpoint
- [ ] IPFS model artifact loading

### Testing
- [ ] Unit test: ONNX model loading and inference
- [ ] Unit test: Feature vector construction
- [ ] Benchmark: Inference latency < 10ms
- [ ] Benchmark: Throughput > 1000 inferences/second
- [ ] Python test: Model export generates valid ONNX file
- [ ] Python test: JOBLIB serialization round-trip

---

## 🗄️ Data Layer Module

### Development
- [ ] Create project: `src/modules/data-layer/DataLayer.csproj`
- [ ] Implement EF Core DbContext with all entity configurations
- [ ] Configure Npgsql provider
- [ ] Implement `IMarketDataRepository`
- [ ] Implement `IFeatureStoreRepository`
- [ ] Implement `IModuleEventRepository`
- [ ] Implement Redis cache service (`IRedisCache`)
- [ ] Implement IPFS service (`IIpfsStorage`)
- [ ] Implement real-time market data streaming WebSocket
- [ ] Subscribe to external market data feeds
- [ ] Implement data transformation pipeline
- [ ] Implement feature computation from raw data

### Testing
- [ ] Unit test: Repository queries with in-memory DB
- [ ] Unit test: Redis cache TTL behavior
- [ ] Unit test: IPFS CID reference management
- [ ] Integration test: PostgreSQL CRUD operations
- [ ] Integration test: Redis pub/sub messaging
- [ ] Performance test: 100,000 ticks/second ingestion

---

## 🔗 Broker Module

### Development
- [ ] Create project: `src/modules/broker/Broker.csproj`
- [ ] Implement `IBrokerClient` interface
- [ ] Implement HYPERLIQUID REST client
- [ ] Implement HYPERLIQUID WebSocket feed client
- [ ] Implement broker fallback chain (Primary → Fallback1 → Fallback2)
- [ ] Implement circuit breaker for each broker connection
- [ ] Implement paper trading mode with order simulation
- [ ] Implement rate limiting per broker API

### Testing
- [ ] Unit test: Broker fallback chain logic
- [ ] Unit test: Circuit breaker behavior
- [ ] Unit test: Paper trading order simulation
- [ ] Integration test: HYPERLIQUID WebSocket connection

---

## 💳 Transactions Module

### Development
- [ ] Create project: `src/modules/transactions/Transactions.csproj`
- [ ] Implement `ITransactionBuilder` — build signed transactions
- [ ] Implement `ITransactionBroadcaster` — submit to blockchain
- [ ] Implement `ITransactionMonitor` — watch for confirmations
- [ ] Implement transaction state machine (pending → submitted → confirmed → failed)
- [ ] Integrate with Nethereum for EVM transactions
- [ ] Validate all addresses from blockchain_addresses table
- [ ] Implement transaction fee estimation
- [ ] Store all transactions in PostgreSQL

### Testing
- [ ] Unit test: Transaction state machine transitions
- [ ] Unit test: Address validation from database
- [ ] Unit test: Fee estimation
- [ ] Integration test: Transaction submission and confirmation

---

## 🌐 Network Modules

### Unique ID Generator
- [ ] Generate UUID v4 for module IDs
- [ ] Generate sequential IDs for tasks (Redis INCR)
- [ ] Thread-safe ID generation

### Task ID Generator
- [ ] Implement `TaskId` struct: `{module_prefix}-{timestamp}-{sequence}`
- [ ] Persist task ID sequences in Redis

### Subscription Manager
- [ ] Implement topic-based pub/sub
- [ ] Implement `ISubscribent` registration/deregistration
- [ ] Support wildcard topic matching

### Runtime Module
- [ ] Docker API integration for module lifecycle management
- [ ] Resource monitoring per module
- [ ] Auto-restart on failure

### Virtual Machine Module
- [ ] Isolated execution environment for untrusted code
- [ ] Sandboxed strategy evaluation

### Container Registry
- [ ] Module container image version tracking
- [ ] Container health status aggregation

### Data-Driven Layer
- [ ] Layer-0 data processing pipeline
- [ ] Real-time feed aggregation
- [ ] Data distribution to all subscribed modules

### Network Mask Module
- [ ] URL registry for all network endpoints
- [ ] Local/global URL resolution
- [ ] Environment-aware endpoint selection

---

## 🔭 Observatory Modules (Metrics & Monitoring)

- [ ] Implement `IMetricsCollector` (OpenTelemetry)
- [ ] Implement performance dashboard data provider
- [ ] Implement module health aggregator
- [ ] Create Mermaid architecture diagrams
- [ ] Generate performance plots (latency histograms, throughput charts)
- [ ] Implement deployment manifests generator
- [ ] Set up Prometheus metrics endpoints on all modules
- [ ] Configure Grafana dashboard for MLS platform

---

## 📄 NuxtJS Documentation Site

- [ ] Initialize NuxtJS 3 in `nuxt-pages/`
- [ ] Configure for GitHub Pages static generation
- [ ] Create index page with project overview
- [ ] Create Architecture page with module topology diagrams
- [ ] Create API Reference page (generated from OpenAPI)
- [ ] Create Payload Schemas page
- [ ] Create Module-by-module documentation
- [ ] Configure Mermaid diagram support
- [ ] Add Algolia search integration
- [ ] Configure `NUXT_PUBLIC_SITE_URL` for deployment

---

## 🔒 Security Checklist

- [ ] No secrets committed to repository
- [ ] All environment variables in `.env` (gitignored)
- [ ] Blockchain addresses loaded from PostgreSQL only
- [ ] Private keys managed by vault/HSM, never in code
- [ ] All WebSocket connections use authentication tokens
- [ ] PostgreSQL user has minimal required permissions
- [ ] Redis password set in production
- [ ] IPFS API port (5001) not exposed in production
- [ ] All Docker containers run as non-root users
- [ ] Dependency vulnerability scan in CI

---

## ⚡ Performance Checklist

- [ ] Trade signal processing: < 1ms end-to-end ✓ Benchmarked
- [ ] ML inference: < 10ms per prediction ✓ Benchmarked
- [ ] Market data ingestion: > 100,000 ticks/second ✓ Benchmarked
- [ ] Database write throughput: > 10,000 records/second ✓ Benchmarked
- [ ] WebSocket broadcast: > 1,000 messages/second per hub ✓ Benchmarked
- [ ] No GC pressure on hot paths (ArrayPool, ObjectPool) ✓ Profiled
- [ ] Memory usage < 512MB per module under load ✓ Measured
- [ ] Startup time < 5 seconds per module ✓ Measured

---

## 🚀 Deployment Checklist

- [ ] All Docker images build successfully
- [ ] Docker Compose starts all services cleanly
- [ ] `mls-network` bridge network created
- [ ] All modules register with Block Controller on startup
- [ ] Health endpoints respond on all modules
- [ ] Staging environment functional
- [ ] GitHub Pages deployment successful
- [ ] CI pipeline passing (build, test, lint)
- [ ] Container images pushed to GHCR
- [ ] NuGet packages published
- [ ] ML models trained and registered
- [ ] ONNX models validated
- [ ] Production environment variables configured

---

## 📊 Progress Summary

| Area | Total | Completed | Progress |
|------|-------|-----------|----------|
| Repository Setup | 9 | 0 | 0% |
| Block Controller | 17 | 0 | 0% |
| Web Application | 21 | 0 | 0% |
| Trader Module | 14 | 0 | 0% |
| Arbitrager Module | 11 | 0 | 0% |
| DeFi Module | 12 | 0 | 0% |
| ML Runtime | 18 | 0 | 0% |
| Data Layer | 14 | 0 | 0% |
| Broker Module | 10 | 0 | 0% |
| Transactions Module | 11 | 0 | 0% |
| Network Modules | 15 | 0 | 0% |
| Observatory | 10 | 0 | 0% |
| NuxtJS Docs | 10 | 0 | 0% |
| Security | 10 | 0 | 0% |
| Performance | 8 | 0 | 0% |
| Deployment | 14 | 0 | 0% |
| **Total** | **194** | **0** | **0%** |
