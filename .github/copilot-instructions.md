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
2. All inter-module messages MUST use the Envelope Protocol: `{ type, version, session_id, module_id, timestamp, payload }`
3. Every module MUST register with Block Controller on startup and send heartbeats every 5 seconds
4. NO Uniswap integrations — use HYPERLIQUID as primary DEX/perpetuals broker
5. All blockchain addresses stored in PostgreSQL, never hardcoded
6. Use named enums and constants in `MLS.Core.Constants` for all magic values
7. Each module runs in its own Docker container on the `mls-network` bridge network
8. All external data pre-defined in typed extension classes before storage

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
| Module | HTTP | WebSocket |
|--------|------|-----------|
| block-controller | 5100 | 6100 |
| web-app | 5200 | 6200 |
| trader | 5300 | 6300 |
| arbitrager | 5400 | 6400 |
| defi | 5500 | 6500 |
| ml-runtime | 5600 | 6600 |
| data-layer | 5700 | 6700 |
| broker | 5800 | 6800 |
| transactions | 5900 | 6900 |

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
