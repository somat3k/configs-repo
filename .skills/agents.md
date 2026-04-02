---
name: agents
source: github/awesome-copilot/skills/declarative-agents + semantic-kernel
description: 'Autonomous agent design patterns for the MLS platform — module agents, trading agents, orchestration agents, and Semantic Kernel agent frameworks.'
---

# Agents — MLS Trading Platform

## Agent Architecture
The MLS platform treats each module as an autonomous agent with:
- **Perception**: Subscribes to WebSocket data feeds
- **Reasoning**: ML inference or rule-based logic
- **Action**: Publishes trade signals or executes transactions
- **Memory**: Redis (short-term) + PostgreSQL (long-term)
- **Communication**: Envelope Protocol via WebSocket mesh

## Semantic Kernel Agents
Use `Microsoft.SemanticKernel.Agents` for AI-driven orchestration:
```csharp
var agent = new ChatCompletionAgent
{
    Name = "TradingAnalyst",
    Instructions = "Analyze market conditions and provide trading recommendations...",
    Kernel = kernel
};

var thread = new AgentGroupChat(tradingAgent, riskAgent, executionAgent)
{
    ExecutionSettings = new() { TerminationStrategy = new MaxIterationTerminationStrategy(5) }
};
```

## Module Agent Pattern
Each module implements `IModuleAgent`:
```csharp
public interface IModuleAgent
{
    string ModuleId { get; }
    string ModuleName { get; }
    Task InitializeAsync(CancellationToken ct);
    Task<AgentStatus> GetStatusAsync();
    Task ProcessMessageAsync(EnvelopePayload envelope, CancellationToken ct);
    IAsyncEnumerable<EnvelopePayload> GetOutputStreamAsync(CancellationToken ct);
}
```

## Orchestration Agent (Block Controller)
Block Controller acts as the orchestration agent:
- Maintains registry of all active module agents
- Routes messages between agents based on subscription topology
- Monitors agent health and triggers failover
- Provides global state coordination

## Subscription/Subscribent Pattern
```csharp
public interface ISubscription<T>
{
    string Topic { get; }
    IAsyncEnumerable<T> GetMessagesAsync(CancellationToken ct);
}

public interface ISubscribent
{
    Task SubscribeAsync(string topic, Func<EnvelopePayload, CancellationToken, Task> handler, CancellationToken ct);
    Task UnsubscribeAsync(string topic);
}
```

## Agent Lifecycle
1. `Initialize` → Load config, connect to storage, register with BlockController
2. `Start` → Begin processing loop, subscribe to data feeds
3. `Running` → Process messages, execute inference, publish results
4. `Pause` → Stop processing but maintain connections
5. `Stop` → Graceful shutdown, persist state, deregister

## Runtime Agent
The `runtime` module manages agent lifecycle for all modules:
- Starts/stops module agents via Docker API
- Monitors resource usage per agent
- Auto-scales based on workload (via multiplication kernel)
- Reports to Block Controller observatory
