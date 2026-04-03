# GitHub Copilot Instructions — MLS Trading Platform

## Project Identity
Machine Learning Studio (MLS) for Trading, Arbitrage, and DeFi — a production-grade, enterprise-level distributed system built in **C# (.NET 9)** with **Blazor** for the web interface. No fake simulations. No placeholder code. Every line must be functional and production-ready.

## Technology Stack
- **Backend**: C# .NET 9, ASP.NET Core, SignalR, Entity Framework Core 9
- **Frontend**: Blazor (Interactive Server + WASM), Microsoft FluentUI Blazor, SignalR client
- **ML**: Python 3.12 (training), C# ONNX Runtime (inference), JOBLIB (Python serialization)
- **Storage**: PostgreSQL 16 (primary), Redis 7 (cache), IPFS/Kubo (distributed artifacts)
- **Orchestration**: .NET Aspire, Docker Compose
- **Documentation Site**: NuxtJS 3 (GitHub Pages)
- **CI/CD**: GitHub Actions

## Architectural Rules (STRICT)
1. Every module MUST host a WebSocket server AND expose an HTTP API
2. All inter-module messages MUST use the Envelope Protocol: `{ block_id, block_sha, block_data, block_state, type, version, unique_id, task_id, session_id, module_id, module_network_address, module_network_port, timestamp, payload }`
3. Every module MUST register with Block Controller on startup and send heartbeats every 5 seconds
4. NO Uniswap integrations — use Camelot, DFYN, nHOP, (more in progress) for arbitrage on Arbitrum Network, HYPERLIQUID as primary DEX/perpetuals broker, Morpho and Balancer as AMM and Supply/Borrow/Collateral Swap/Repay mechanics.
5. All blockchain addresses stored in PostgreSQL as resources, tokens, infrastructure, never hardcoded
6. Use named enums and constants in `MLS.Core.Constants` for all magic values
7. Each module runs in its own Docker container on the `mls-network` bridge network
8. All external data pre-defined in typed extension classes before storage
9. Extensive presence of Invokers in framework-based architecture
10. Modules code-structures writen in compact architecture
11. Modules must run online at sesssion end with clear documentation and all ports data-driven online

## Coding Standards
- Target framework: `net9.0`
- Nullable reference types: enabled
- C# version: 13
- Use primary constructors for dependency injection
- Use `IAsyncEnumerable<T>` for all streaming operations
- Use `Channel<T>` for all producer/consumer scenarios
- No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in async code
- Use `ConfigureAwait(false)` in all library/infrastructure code
- XML documentation on all public APIs

## Module Port Allocation
|      Module      | HTTP | WebSocket |
|------------------|------|-----------|
| block-controller | 5100 |    6100   |
| web-app          | 5200 |    6200   |
| trader           | 5300 |    6300   |
| arbitrager       | 5400 |    6400   |
| defi             | 5500 |    6500   |
| ml-runtime       | 5600 |    6600   |
| data-layer       | 5700 |    6700   |
| broker           | 5800 |    6800   |  
| transactions     | 5900 |    6900   |
| shell-vm         | 5950 |    6950   |

## Namespace Conventions
- Core shared: `MLS.Core.{Feature}`
- Module: `MLS.{ModuleName}.{Feature}` (e.g., `MLS.Trader.Execution`)
- Tests: `MLS.{ModuleName}.Tests.{Feature}`
- Contracts: `MLS.Contracts.{PayloadType}`

## Testing Requirements
- Framework: xUnit + FluentAssertions + Moq
- Coverage target: >80%
- All WebSocket communication must have integration tests
- Use `Aspire.Hosting.Testing` for AppHost integration tests
- Benchmarks with BenchmarkDotNet in `src/benchmarks/`

## Skills Reference
When generating code, apply the relevant skill from `.skills/`:
/// Adding skills for references aligned to github/awesome-copilot
- Copilot Blueprint: `.skills/copilot-instruction-blueprint.md`
- C#/.NET: `.skills/dotnet-devs.md`
- Blazor UI: `.skills/premium-uiux-blazor.md` + `.skills/web-apps.md`
- Architecture: `.skills/system-architect.md`
- ML/AI (core): `.skills/machine-learning.md` + `.skills/artificial-intelligence.md`
- Storage: `.skills/storage-data-management.md`
- WebSockets: `.skills/websockets-inferences.md`
- Web3/DeFi: `.skills/web3.md`
- Networking: `.skills/networking.md`
- Python: `.skills/python.md`
- Performance: `.skills/beast-development.md`
- Agents: `.skills/agents.md`

### AI Sub-Components (two separate concerns)
- **System Acceleration** (L1/L2/L3/L4 + CPU thread control): `.skills/acceleration/acceleration.md`
- **Trading Model** (`model-t`, trader module): `.skills/models/model-t.md`
- **Arbitrage Model** (`model-a`, arbitrager module): `.skills/models/model-a.md`
- **DeFi Model** (`model-d`, defi module): `.skills/models/model-d.md`

### Model Naming Conventions
| Enum Value | Python Class | File Prefix | DB Key | Consumer Module |
|-----------|-------------|------------|--------|----------------|
| `ModelType.Trading` | `ModelT` | `model_t_` | `model-t` | `trader` |
| `ModelType.Arbitrage` | `ModelA` | `model_a_` | `model-a` | `arbitrager` |
| `ModelType.DeFi` | `ModelD` | `model_d_` | `model-d` | `defi` |

## Module Port Allocation (Updated — includes Designer and AI Hub)

|      Module      | HTTP | WebSocket | Status |
|------------------|------|-----------|--------|
| block-controller | 5100 |    6100   | Existing |
| web-app          | 5200 |    6200   | Existing |
| **designer**     | **5250** | **6250** | **New** |
| trader           | 5300 |    6300   | Existing |
| arbitrager       | 5400 |    6400   | Existing |
| defi             | 5500 |    6500   | Existing |
| ml-runtime       | 5600 |    6600   | Existing |
| data-layer       | 5700 |    6700   | Existing |
| **ai-hub**       | **5750** | **6750** | **New** |
| broker           | 5800 |    6800   | Existing |
| transactions     | 5900 |    6900   | Existing |
| shell-vm         | 5950 |    6950   | Existing |

## Designer Module Rules

> Reference: `.skills/designer.md` · `docs/architecture/designer-block-graph.md` · `.github/copilot-rules/rule-designer-blocks.md`

- Every new block type MUST implement `IBlockElement` and register with `BlockRegistry` on startup
- Block sockets MUST be typed using `BlockSocketType` enum — no untyped object connections
- `ICompositionGraph.ConnectAsync` MUST throw `InvalidBlockConnectionException` on socket type mismatch
- Composition blocks MUST expose disconnected inner sockets as outer ports via `GetExposedPorts()` (fractal nesting)
- `SchemaVersion` MUST be incremented on every structural strategy change
- All blockchain addresses via `IBlockchainAddressBook` enum — zero hardcoded strings anywhere in adapters
- `TrainModelBlock` emits `TRAINING_JOB_START` — it NEVER calls Python directly
- Exchange adapters: NO Uniswap — only Camelot, DFYN, Balancer, Morpho, Hyperliquid (Arbitrum)

## AI Hub Rules

> Reference: `.skills/ai-hub.md` · `docs/architecture/ai-hub-providers.md`

- All LLM providers MUST implement `ILLMProvider` (wraps `IChatCompletionService`)
- Every `[KernelFunction]` MUST have `[Description("...")]` and every parameter MUST have `[Description("...")]`
- Canvas actions MUST go through `CanvasActionDispatcher` — NEVER directly mutate UI state
- Context assembly MUST complete in < 200ms (parallel queries, individual timeouts)
- Canvas-producing plugin functions MUST dispatch `CanvasAction` BEFORE returning their string result
- State-modifying functions (PlaceOrder) MUST require `confirmed: bool` parameter
- Streaming responses use `kernel.InvokeStreamingAsync` — NEVER buffer complete response

## Performance Rules

> Reference: `.skills/acceleration/acceleration.md` · `.skills/beast-development.md` · `docs/architecture/performance-semantics.md`

- Envelope routing hot path: ZERO allocation — use `ArrayPool<byte>`, `Span<byte>`, pre-allocated `OrtValue`
- `MessagePack` for ALL wire serialization in production (JSON only for human-readable config files)
- All `Channel<T>` consumers MUST use `BoundedChannelOptions` with explicit `FullMode` policy
- `BenchmarkDotNet` test REQUIRED for any method on the envelope routing or indicator computation hot path
- GC mode: Server GC for all backend services (`"System.GC.Server": true` in runtimeconfig)
- Python training: `torch.compile(mode="max-autotune")` + AMP bf16 + `DataLoader(num_workers=4, pin_memory=True)`

## Block Controller Standalone Node Rules

> Reference: `.skills/websockets-inferences.md` · `.skills/networking.md` · `src/block-controller/docs/sessions/SESSION-3.md`

- Block Controller hub endpoint: `ws://block-controller:6100/hubs/block-controller`
- Module connections MUST use query param `?moduleId=<guid>` — joined to their own group on connect
- External client connections use `?clientId=<guid>` — same bidirectional protocol
- ALL connections automatically join the `broadcast` group (platform-wide events)
- Dynamic topic subscription: `SubscribeToTopicAsync(topic)` / `UnsubscribeFromTopicAsync(topic)`
- Hub methods: `SendEnvelope` (client → hub), `ReceiveEnvelope` (hub → client)
- `IMessageRouter.BroadcastAsync` sends to `Group("broadcast")` — NOT `Clients.All`
- Block Controller MUST start and accept connections with NO other modules running
