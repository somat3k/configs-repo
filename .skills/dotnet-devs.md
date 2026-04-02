---
name: dotnet-devs
source: github/awesome-copilot/skills/dotnet-best-practices + csharp-async
description: '.NET/C# development best practices for the MLS Trading Platform — covers architecture patterns, async programming, DI, testing, EF Core, and Semantic Kernel integration.'
---

# .NET/C# Developer Skills — MLS Trading Platform

## Project Context
This skill applies to all C#/.NET modules in the Machine Learning Studio (MLS) for Trading, Arbitrage, and DeFi. All modules target **net9.0**, use **Microsoft.AspNetCore.SignalR** for WebSocket communication, and integrate with **Redis**, **PostgreSQL**, and **IPFS** storage layers.

## Documentation & Structure
- Create comprehensive XML documentation comments for all public classes, interfaces, methods, and properties
- Follow the established namespace structure: `MLS.{ModuleName}.{Feature}` (e.g., `MLS.Trader.Execution`, `MLS.BlockController.Network`)
- Each module lives in `src/{module-name}/` with its own `.csproj` and `docs/` folder

## Design Patterns & Architecture
- Use primary constructor syntax for dependency injection
- Implement the Command Handler pattern with generic base classes (`CommandHandler<TOptions>`)
- Use interface segregation — prefix interfaces with `I` (e.g., `IModuleNode`, `IPayloadHandler`)
- Follow Factory pattern for complex object creation (e.g., `ModuleNodeFactory`)
- Apply **Lean Methodology**: minimal code, maximum function; no fake simulations or placeholder code
- Use **named enums and constants** for all cross-module constants and reusability (e.g., `ModuleConstants.DefaultWsPort`)

## Module Communication Pattern
Every module must:
- Host a WebSocket server on its designated port
- Expose an HTTP API for inference and command endpoints
- Report status payloads to BlockController on a regular heartbeat interval
- Use the **Envelope Protocol**: `{ type, version, session_id, payload }` for all inter-module messages
- Register itself in the Module Registry on startup

## Async/Await Patterns
- Use the `Async` suffix for all async methods
- Return `Task<T>` or `ValueTask<T>` for high-performance scenarios
- Use `IAsyncEnumerable<T>` for streaming data (market feeds, ML inferences)
- Never use `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()` in async code
- Use `CancellationToken` in all long-running operations
- Apply `ConfigureAwait(false)` in library/infrastructure code

## Testing Standards
- Use **xUnit** as the primary test framework
- Use **FluentAssertions** for readable assertions
- Use **Moq** for mocking dependencies
- Follow AAA pattern (Arrange, Act, Assert)
- Test both success and failure scenarios
- Include WebSocket communication integration tests
- Use `Aspire.Hosting.Testing` for AppHost integration tests

## Configuration & Settings
- Use strongly-typed configuration classes with data annotations
- Bind from `appsettings.json` and environment variables
- Use `IOptions<T>` pattern for injecting settings
- Support per-module environment variable overrides: `MLS_{MODULE}_{SETTING}`

## Semantic Kernel & AI Integration
- Use `Microsoft.SemanticKernel` for ML inference pipelines
- Implement proper kernel configuration with `IKernelBuilder`
- Use ONNX runtime for exported ML models
- Export ML models as both ONNX and JOBLIB formats

## Error Handling & Logging
- Use structured logging with `Microsoft.Extensions.Logging`
- Use named log scopes: `using (_logger.BeginScope(new { ModuleId, SessionId }))`
- Include module name and session ID in all log entries
- Implement circuit breaker pattern for inter-module communication failures

## Performance & Security
- Use C# 13+ features and .NET 9 optimizations
- Use `System.Collections.Concurrent` for thread-safe collections
- Use `ArrayPool<T>` and `MemoryPool<T>` for high-throughput buffer management
- Implement proper input validation and sanitization
- Use parameterized queries for all database operations
