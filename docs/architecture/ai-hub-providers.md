# AI Hub — Multi-Provider Architecture

> **Reference**: [Giga-Scale Plan](giga-scale-plan.md) | [Session Schedule](../session-schedule.md) (Sessions 08–10)
> **Module**: `ai-hub` · HTTP `:5750` · WebSocket `:6750`

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        AI-HUB MODULE (MLS.AIHub :5750/:6750)                    │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                    CONTEXT ASSEMBLER                                     │    │
│  │                                                                          │    │
│  │  ProjectAwarenessContext:                                                │    │
│  │  ├── ModuleRegistry state (all modules, health, ports, last heartbeat)  │    │
│  │  ├── Active StrategyGraph (current designer session + block status)     │    │
│  │  ├── Open positions + P&L (from Trader module)                          │    │
│  │  ├── Active arbitrage opportunities (from Arbitrager, last 60s)        │    │
│  │  ├── DeFi position health factors (from DeFi module)                   │    │
│  │  ├── ML model registry + inference metrics (latency, accuracy)         │    │
│  │  ├── Recent envelope stream (last 50 messages by type, configurable)   │    │
│  │  ├── Shell VM session logs (last 1000 lines per active session)        │    │
│  │  └── User MDI canvas layout (open panels + their current data)         │    │
│  │                                              Target: < 200ms assembly  │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │               PROVIDER ROUTER (User-Defined Distributor)                │    │
│  │                                                                          │    │
│  │  ILLMProvider (wraps Semantic Kernel IChatCompletionService)            │    │
│  │  ├── OpenAIProvider        → GPT-4o, GPT-4-turbo, o3, o3-mini          │    │
│  │  ├── AnthropicProvider     → Claude 3.5 Sonnet, Claude 3 Opus           │    │
│  │  ├── GoogleProvider        → Gemini 2.5 Pro, Gemini 2.0 Flash           │    │
│  │  ├── GroqProvider          → Llama3-70b, Mixtral-8x7b (low latency)    │    │
│  │  ├── OpenRouterProvider    → Unified routing: 100+ models               │    │
│  │  ├── VercelAIProvider      → AI SDK compatible edge endpoint            │    │
│  │  └── LocalProvider         → Ollama / llama.cpp (offline / air-gapped) │    │
│  │                                                                          │    │
│  │  Selection: persisted in PostgreSQL user_prefs per user ID              │    │
│  │  Fallback chain: Primary → Secondary → Local (all configurable)        │    │
│  │  Per-query override: AI_QUERY.provider_override field                  │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │               MLS TOOL REGISTRY (Semantic Kernel Plugins)               │    │
│  │                                                                          │    │
│  │  TradingPlugin      → positions, signals, P&L, order placement          │    │
│  │  DesignerPlugin     → create/modify/explain/backtest strategies         │    │
│  │  AnalyticsPlugin    → plot chart, SHAP, P&L report, SQL query           │    │
│  │  MLRuntimePlugin    → train model, get metrics, deploy to production    │    │
│  │  DeFiPlugin         → health factors, simulate rebalance, APYs          │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │               CANVAS MANIPULATION ENGINE                                 │    │
│  │                                                                          │    │
│  │  AI function result → CanvasAction[] → AI_CANVAS_ACTION envelope        │    │
│  │  → web-app SignalR → AICanvasService → MDI window manager               │    │
│  │                                                                          │    │
│  │  Action types: OpenPanel, UpdateChart, HighlightBlock, ShowDiagram,    │    │
│  │                AddAnnotation, OpenDesignerGraph                         │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Provider Configuration

### ILLMProvider Interface

```csharp
/// <summary>Wraps a Semantic Kernel IChatCompletionService with MLS metadata.</summary>
public interface ILLMProvider
{
    string ProviderId { get; }                    // "openai", "anthropic", etc.
    string DisplayName { get; }
    IReadOnlyList<string> SupportedModels { get; }
    bool IsAvailable { get; }
    Task<bool> CheckAvailabilityAsync(CancellationToken ct);
    IChatCompletionService BuildService(string modelId);
    IStreamingChatCompletionService BuildStreamingService(string modelId);
}
```

### Provider Capabilities Matrix

| Provider | Models | Streaming | Function Calling | Context Window | Use Case |
|----------|--------|-----------|-----------------|----------------|----------|
| OpenAI | GPT-4o, o3, o3-mini | ✅ | ✅ Native | 128k | Default high quality |
| Anthropic | Claude 3.5 Sonnet, Opus | ✅ | ✅ Native | 200k | Long context, analysis |
| Google | Gemini 2.5 Pro, Flash | ✅ | ✅ Native | 1M | Very long context |
| Groq | Llama3-70b, Mixtral | ✅ | ✅ | 8k–32k | Ultra-low latency |
| OpenRouter | Any 100+ | ✅ | Varies | Varies | Multi-model routing |
| VercelAI | Provider-dependent | ✅ | ✅ | Varies | Edge deployment |
| Local | Ollama models | ✅ | Varies | Varies | Offline / air-gapped |

### User-Defined Distributor

```csharp
/// <summary>Routes AI queries to the user-configured provider with fallback chain.</summary>
public sealed class ProviderRouter(
    IEnumerable<ILLMProvider> _providers,
    IUserPreferenceRepository _prefs,
    ILogger<ProviderRouter> _logger
) : IProviderRouter
{
    public async Task<ILLMProvider> SelectProviderAsync(AIQueryPayload query, Guid userId, CancellationToken ct)
    {
        var prefs = await _prefs.GetAsync(userId, ct);

        // Per-query override takes highest priority
        if (query.ProviderOverride is { } overrideId)
        {
            var overrideProvider = _providers.FirstOrDefault(p => p.ProviderId == overrideId);
            if (overrideProvider?.IsAvailable == true) return overrideProvider;
        }

        // Walk user-configured fallback chain
        foreach (var providerId in prefs.FallbackChain)
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);
            if (provider is null) continue;
            if (await provider.CheckAvailabilityAsync(ct)) return provider;
            _logger.LogWarning("Provider {Id} unavailable, trying next in chain", providerId);
        }

        // Always-available Local provider as final fallback
        return _providers.Single(p => p.ProviderId == "local");
    }
}
```

---

## Semantic Kernel Plugin Reference

### TradingPlugin

```csharp
[KernelFunction, Description("Get all open trading positions with current unrealised P&L")]
public async Task<string> GetPositions([Description("Filter by symbol")] string? symbol = null) {}

[KernelFunction, Description("Place a market or limit order on the configured exchange")]
public async Task<string> PlaceOrder(
    [Description("Trading symbol (e.g. BTC-PERP)")] string symbol,
    [Description("BUY or SELL")] string side,
    [Description("Order quantity in base currency")] decimal quantity,
    [Description("Optional limit price; if omitted, market order")] decimal? limitPrice = null) {}

[KernelFunction, Description("Get recent ML trading signal history for a symbol")]
public async Task<string> GetSignalHistory(
    [Description("Symbol")] string symbol,
    [Description("Number of recent signals to return")] int count = 20) {}

[KernelFunction, Description("Get P&L summary for a given period")]
public async Task<string> GetPnL(
    [Description("Period: today, 7d, 30d, ytd, all")] string period = "30d") {}
```

### DesignerPlugin

```csharp
[KernelFunction, Description("Create a new trading strategy from a named template")]
public async Task<string> CreateStrategy(
    [Description("Display name for the new strategy")] string name,
    [Description("Template name from designer-templates/")] string templateName) {}

[KernelFunction, Description("Add a block to the currently active designer canvas")]
public async Task<string> AddBlock(
    [Description("Block type name (e.g. RSIBlock, ModelTInferenceBlock)")] string blockType,
    [Description("JSON object of block parameters")] string jsonParameters = "{}") {}

[KernelFunction, Description("Run historical backtest on a strategy")]
public async Task<string> RunBacktest(
    [Description("Strategy ID (UUID)")] Guid strategyId,
    [Description("ISO 8601 start date")] string from,
    [Description("ISO 8601 end date")] string to) {}

[KernelFunction, Description("Explain a strategy in plain language, describing each block and connection")]
public async Task<string> ExplainStrategy(
    [Description("Strategy ID (UUID)")] Guid strategyId) {}

[KernelFunction, Description("List all available block types with descriptions")]
public async Task<string> ListBlockTypes(
    [Description("Domain filter: trading, arbitrage, defi, ml-training, data-hydra")] string? domain = null) {}
```

### AnalyticsPlugin

```csharp
[KernelFunction, Description("Open a live price chart for a symbol on the canvas")]
public async Task<string> PlotChart(
    [Description("Symbol (e.g. BTC-PERP, ETH-PERP)")] string symbol,
    [Description("Timeframe: 1m, 5m, 15m, 1h, 4h, 1d")] string timeframe = "1h") {}

[KernelFunction, Description("Generate a SHAP feature importance visualisation for an ML model")]
public async Task<string> GenerateSHAP(
    [Description("Model ID from registry")] string modelId) {}

[KernelFunction, Description("Export a performance report as a canvas panel")]
public async Task<string> ExportReport(
    [Description("Report type: pnl, drawdown, trades, sharpe")] string reportType,
    [Description("Period: today, 7d, 30d, ytd, all")] string period = "30d") {}

[KernelFunction, Description("Query the data layer using natural language converted to SQL")]
public async Task<string> AskAboutData(
    [Description("Natural language query about market data, positions, or models")] string query) {}
```

### MLRuntimePlugin

```csharp
[KernelFunction, Description("Initiate a model training job via the Shell VM")]
public async Task<string> TrainModel(
    [Description("Model type: model-t, model-a, model-d")] string modelType,
    [Description("JSON hyperparameter overrides")] string configJson = "{}") {}

[KernelFunction, Description("Get performance metrics for a registered model")]
public async Task<string> GetModelMetrics(
    [Description("Model ID from registry")] string modelId) {}

[KernelFunction, Description("Promote a validated model to production slot")]
public async Task<string> DeployModel(
    [Description("Model ID to promote to production")] string modelId) {}
```

### DeFiPlugin

```csharp
[KernelFunction, Description("Get current health factors for all DeFi lending positions")]
public async Task<string> GetHealthFactors() {}

[KernelFunction, Description("Simulate a DeFi rebalancing operation without execution")]
public async Task<string> SimulateRebalance(
    [Description("JSON rebalancing parameters")] string paramsJson) {}

[KernelFunction, Description("Get current APY rates across all supported protocols")]
public async Task<string> GetPoolAPYs(
    [Description("Comma-separated protocol names: morpho, balancer, all")] string protocols = "all") {}
```

---

## Canvas Action Types

The AI Hub dispatches canvas actions after resolving plugin function results:

```csharp
public abstract record CanvasAction;

/// Open a new MDI panel with pre-loaded data
public sealed record OpenPanelAction(
    string PanelType,          // "TradingChart", "PnLReport", "DesignerCanvas", etc.
    JsonElement Data,          // Initialisation data for the panel
    string? Title = null       // Optional panel title override
) : CanvasAction;

/// Push new series data to an existing chart
public sealed record UpdateChartAction(
    Guid ChartId,
    string SeriesName,
    double[] Values,
    DateTimeOffset[] Timestamps
) : CanvasAction;

/// Pulse animation on a designer block
public sealed record HighlightBlockAction(
    Guid BlockId,
    string Color,              // CSS color
    int DurationMs = 2000
) : CanvasAction;

/// Render a Mermaid diagram in a new canvas panel
public sealed record ShowDiagramAction(
    string MermaidSource,
    string Title
) : CanvasAction;

/// Add a labelled annotation marker on a chart
public sealed record AddAnnotationAction(
    Guid ChartId,
    DateTimeOffset Time,
    string Label,
    string Color = "#00d4ff"
) : CanvasAction;

/// Load and display a strategy graph in the designer canvas
public sealed record OpenDesignerGraphAction(
    JsonElement StrategySchema
) : CanvasAction;
```

---

## AI Chat Streaming Pipeline

```
AI_QUERY envelope received by AIHub
  │
  ├── Step 1: ContextAssembler.AssembleAsync()    ← parallel queries to 6+ sources
  │           ProjectSnapshot assembled in < 200ms
  │
  ├── Step 2: ProviderRouter.SelectProviderAsync() ← check user prefs + availability
  │
  ├── Step 3: SK Kernel.InvokeStreamingAsync(
  │              messages: [SystemPrompt(snapshot) + UserQuery],
  │              settings: { temperature=0.1 }     ← low temp for tool accuracy
  │           )
  │
  ├── Step 4: For each streaming token/function result:
  │           ├── If text chunk → AI_RESPONSE_CHUNK → web-app SignalR
  │           └── If function result → CanvasActionDispatcher.DispatchAsync(result)
  │                                  → AI_CANVAS_ACTION → web-app SignalR (parallel)
  │
  └── Step 5: AI_RESPONSE_COMPLETE → web-app SignalR
```

### System Prompt Template

```
You are the MLS Platform AI Assistant with full awareness of the current system state.

## Platform Status
- Active modules: {module_list_with_health}
- Active strategy: {strategy_name} ({strategy_status})

## Open Positions
{positions_summary}

## Available Tools
You have access to: TradingPlugin, DesignerPlugin, AnalyticsPlugin, MLRuntimePlugin, DeFiPlugin
Use tools to take real actions on the platform, not just describe them.

## Key Rules
- Always use tools to retrieve live data before answering data questions
- Canvas actions (charts, panels) happen automatically via your tool calls
- For trading actions, confirm the user's intent before calling PlaceOrder
- Model training jobs take several minutes — acknowledge this to the user
```

---

## Configuration

```json
{
  "AIHub": {
    "DefaultProvider": "openai",
    "DefaultModel": "gpt-4o",
    "FallbackChain": ["openai", "anthropic", "groq", "local"],
    "ContextAssembly": {
      "TimeoutMs": 200,
      "MaxEnvelopeHistory": 50,
      "MaxShellLogLines": 1000
    },
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

## See Also

- [Session Schedule — Sessions 08–10](../session-schedule.md#phase-2--ai-hub)
- [Canvas MDI Layout](canvas-mdi-layout.md) — how canvas actions render in MDI panels
- [Designer Block Graph](designer-block-graph.md) — block types the AI can create
- [Giga-Scale Plan](giga-scale-plan.md) — full system context
