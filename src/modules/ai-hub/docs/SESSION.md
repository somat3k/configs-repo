# AI Hub Module — Session Prompt

> Use this document as context when generating AI Hub module code with GitHub Copilot.
> Cross-reference: [AI Hub Providers](../../docs/architecture/ai-hub-providers.md) | [Session Schedule — Phase 2](../../docs/session-schedule.md#phase-2--ai-hub)

---

## 1. Module Identity

| Field | Value |
|---|---|
| **Name** | `ai-hub` |
| **Namespace** | `MLS.AIHub` |
| **Role** | Multi-provider AI orchestration with complete project awareness, Semantic Kernel plugins, and canvas manipulation |
| **HTTP Port** | `5750` |
| **WebSocket Port** | `6750` |
| **Container** | `mls-ai-hub` |
| **Docker image** | `ghcr.io/somat3k/mls-ai-hub:latest` |

---

## 2. Technology Stack

| Technology | Purpose |
|---|---|
| .NET 9 ASP.NET Core | Module host, REST + SSE streaming API |
| Microsoft.SemanticKernel | Agent framework, plugin orchestration, function calling |
| Microsoft.SemanticKernel.Connectors.* | Per-provider SK connectors |
| SignalR | Stream AI response chunks to web-app |
| MessagePack-CSharp | Envelope wire serialization |
| Npgsql + EF Core 9 | User preferences, conversation history |

---

## 3. Project Structure

```
MLS.AIHub/
├── Providers/
│   ├── ILLMProvider.cs
│   ├── OpenAIProvider.cs          → GPT-4o, o3, o3-mini
│   ├── AnthropicProvider.cs       → Claude 3.5 Sonnet, Claude 3 Opus
│   ├── GoogleProvider.cs          → Gemini 2.5 Pro, Gemini Flash
│   ├── GroqProvider.cs            → Llama3-70b, Mixtral
│   ├── OpenRouterProvider.cs      → 100+ model routing
│   ├── VercelAIProvider.cs        → AI SDK edge endpoint
│   └── LocalProvider.cs           → Ollama / llama.cpp
│
├── Services/
│   ├── ProviderRouter.cs          ← User-defined distributor
│   ├── ChatService.cs             ← Streaming: AI_QUERY → chunks
│   └── ConversationRepository.cs
│
├── Plugins/
│   ├── TradingPlugin.cs           ← GetPositions, PlaceOrder, GetSignalHistory, GetPnL
│   ├── DesignerPlugin.cs          ← CreateStrategy, AddBlock, RunBacktest, ExplainStrategy
│   ├── AnalyticsPlugin.cs         ← PlotChart, GenerateSHAP, ExportReport, AskAboutData
│   ├── MLRuntimePlugin.cs         ← TrainModel, GetModelMetrics, DeployModel
│   └── DeFiPlugin.cs              ← GetHealthFactors, SimulateRebalance, GetPoolAPYs
│
├── Context/
│   ├── ContextAssembler.cs        ← Assembles ProjectSnapshot in < 200ms
│   └── ProjectSnapshot.cs         ← Typed: modules, positions, strategies, models
│
├── Canvas/
│   ├── CanvasAction.cs            ← Discriminated union of all action types
│   └── CanvasActionDispatcher.cs  ← SK function result → CanvasAction → envelope
│
├── Controllers/
│   └── ChatController.cs          ← POST /api/chat, GET /api/chat/stream (SSE)
│
└── Hubs/
    └── AIHubSignalR.cs            ← SignalR: AI_RESPONSE_CHUNK, AI_CANVAS_ACTION
```

---

## 4. Key Interfaces

```csharp
/// Provider wrapping Semantic Kernel IChatCompletionService
public interface ILLMProvider
{
    string ProviderId { get; }
    IReadOnlyList<string> SupportedModels { get; }
    bool IsAvailable { get; }
    Task<bool> CheckAvailabilityAsync(CancellationToken ct);
    IChatCompletionService BuildService(string modelId);
    IStreamingChatCompletionService BuildStreamingService(string modelId);
}

/// Assembles live project state as AI context
public interface IContextAssembler
{
    /// Target: &lt; 200ms. Queries all modules in parallel.
    Task<ProjectSnapshot> AssembleAsync(Guid userId, CancellationToken ct);
}

/// Dispatch canvas action to web-app via SignalR
public interface ICanvasActionDispatcher
{
    Task DispatchAsync(CanvasAction action, Guid userId, CancellationToken ct);
}
```

---

## 5. Semantic Kernel Plugin Rules

- Every `[KernelFunction]` MUST have `[Description("...")]` — required for AI tool discovery
- Every parameter MUST have `[Description("...")]` — required for AI argument filling
- All plugin methods must be `async Task<string>` — string result is the AI's function output
- Canvas-producing functions (PlotChart, etc.) MUST call `ICanvasActionDispatcher.DispatchAsync` BEFORE returning
- Trading actions that modify state (PlaceOrder) MUST be confirmed (add `confirmed: bool` parameter)

```csharp
// Template for a new plugin function
[KernelFunction, Description("Clear description of what this function does and what it returns")]
public async Task<string> FunctionName(
    [Description("What this parameter means and its format")] string param1,
    [Description("Optional: describe what happens if omitted")] string? optionalParam = null,
    CancellationToken ct = default)
{
    // 1. Call relevant MLS module via HTTP/WS
    // 2. If canvas action: await _canvasDispatcher.DispatchAsync(action, userId, ct)
    // 3. Return human-readable string summary of result
}
```

---

## 6. Canvas Action Types

```csharp
public abstract record CanvasAction;
public sealed record OpenPanelAction(string PanelType, JsonElement Data, string? Title = null) : CanvasAction;
public sealed record UpdateChartAction(Guid ChartId, string SeriesName, double[] Values, DateTimeOffset[] Timestamps) : CanvasAction;
public sealed record HighlightBlockAction(Guid BlockId, string Color, int DurationMs = 2000) : CanvasAction;
public sealed record ShowDiagramAction(string MermaidSource, string Title) : CanvasAction;
public sealed record AddAnnotationAction(Guid ChartId, DateTimeOffset Time, string Label, string Color = "#00d4ff") : CanvasAction;
public sealed record OpenDesignerGraphAction(JsonElement StrategySchema) : CanvasAction;
```

---

## 7. Envelope Types Produced

| Envelope | When |
|---|---|
| `AI_RESPONSE_CHUNK` | Each token chunk in streaming response |
| `AI_CANVAS_ACTION` | Each canvas action dispatched by plugins |
| `AI_RESPONSE_COMPLETE` | Final token sent |

## 8. Envelope Types Consumed

| Envelope | Action |
|---|---|
| `AI_QUERY` | Process: assemble context → select provider → SK invoke |

---

## 9. ContextAssembler Sources

All queried in parallel within 200ms timeout:

```
1. Block Controller:  GET /api/modules                   (all modules + health)
2. Trader:            GET /api/positions                  (open positions + P&L)
3. Trader:            GET /api/signals/recent?n=50        (recent ML signals)
4. Arbitrager:        GET /api/opportunities/active       (current arb opportunities)
5. DeFi:              GET /api/positions/health           (health factors)
6. ML Runtime:        GET /api/models                     (registered models + metrics)
7. Designer:          GET /api/strategies/active          (active strategy graph)
8. Block Controller:  GET /api/envelopes/recent?n=50      (recent envelope stream)
9. Shell VM:          GET /api/sessions/active/logs       (active session output)
10. Web App:          GET /api/canvas/layout/{userId}     (open panels layout)
```

---

## 10. Configuration

```json
{
  "MLS": {
    "Module": "ai-hub",
    "HttpPort": 5750,
    "WebSocketPort": 6750,
    "Network": {
      "BlockControllerUrl": "http://block-controller:5100",
      "BlockControllerWsUrl": "ws://block-controller:6100/ws/hub",
      "TraderUrl": "http://trader:5300",
      "ArbitragerUrl": "http://arbitrager:5400",
      "DeFiUrl": "http://defi:5500",
      "MLRuntimeUrl": "http://ml-runtime:5600",
      "DesignerUrl": "http://designer:5250",
      "ShellVMUrl": "http://shell-vm:5950",
      "WebAppUrl": "http://web-app:5200"
    }
  },
  "AIHub": {
    "DefaultProvider": "openai",
    "DefaultModel": "gpt-4o",
    "FallbackChain": ["openai", "anthropic", "groq", "local"],
    "ContextAssembly": { "TimeoutMs": 200, "MaxEnvelopeHistory": 50 },
    "Providers": {
      "OpenAI": { "ApiKey": "" },
      "Anthropic": { "ApiKey": "" },
      "Google": { "ApiKey": "" },
      "Groq": { "ApiKey": "" },
      "OpenRouter": { "ApiKey": "" },
      "VercelAI": { "BaseUrl": "" },
      "Local": { "OllamaBaseUrl": "http://localhost:11434", "DefaultModel": "llama3" }
    }
  }
}
```

---

## 11. Skills to Apply

- `.skills/ai-hub.md` — SK plugin patterns, provider routing, canvas actions
- `.skills/semantic-kernel.md` — SK agent framework, function calling, streaming
- `.skills/agents.md` — module agent pattern, IModuleAgent
- `.skills/dotnet-devs.md` — C# 13, DI, async, primary constructors
- `.skills/websockets-inferences.md` — SignalR streaming hub
- `.skills/networking.md` — Block Controller registration, service discovery
