---
name: ai-hub
source: custom (MLS Trading Platform)
description: 'AI Hub patterns for the MLS platform — Semantic Kernel plugin design, multi-provider routing, context assembly, canvas action dispatch, and streaming AI responses.'
---

# AI Hub — MLS Trading Platform

> Apply this skill when working on: `MLS.AIHub`, Semantic Kernel plugins, provider routing, AI chat panel, canvas actions, or AI-driven workflow automation.

---

## Semantic Kernel Plugin Rules

### Function Declaration

```csharp
// ALL KernelFunction must have:
// 1. [Description] attribute — required for AI tool discovery
// 2. Parameter [Description] attributes — required for AI argument filling
// 3. Return type Task<string> — string result is the AI's tool output
// 4. XML documentation

/// <summary>Short description of the function.</summary>
[KernelFunction, Description("Clear description of what this function does")]
public async Task<string> FunctionName(
    [Description("What this parameter means, its format and constraints")] string param,
    CancellationToken ct = default)
{
    // Implementation
}
```

### Canvas-Producing Functions

Functions that should open panels or update the canvas MUST dispatch canvas actions BEFORE returning their string result:

```csharp
[KernelFunction, Description("Open a live price chart for a symbol on the canvas")]
public async Task<string> PlotChart(
    [Description("Trading symbol (e.g. BTC-PERP)")] string symbol,
    [Description("Timeframe: 1m, 5m, 15m, 1h, 4h, 1d")] string timeframe = "1h",
    CancellationToken ct = default)
{
    // 1. Dispatch canvas action FIRST (opens panel in parallel with text response)
    await _canvasDispatcher.DispatchAsync(
        new OpenPanelAction("TradingChart", JsonSerializer.SerializeToElement(new { symbol, timeframe })),
        _userId, ct);

    // 2. Return text summary
    return $"Opened {symbol} {timeframe} chart. Fetching live candle data...";
}
```

### State-Modifying Functions

Functions that modify live trading state MUST require explicit user confirmation:

```csharp
[KernelFunction, Description("Place a trading order. User must confirm before execution.")]
public async Task<string> PlaceOrder(
    [Description("Symbol (e.g. BTC-PERP)")] string symbol,
    [Description("BUY or SELL")] string side,
    [Description("Order quantity in base currency")] decimal quantity,
    [Description("Set to true only after user has confirmed the order details")] bool confirmed = false,
    CancellationToken ct = default)
{
    if (!confirmed)
        return $"Please confirm: {side} {quantity} {symbol}. Reply 'confirm' to execute.";

    // Execute after confirmation
    var result = await _traderClient.PlaceOrderAsync(symbol, side, quantity, ct);
    return $"Order placed: {result.OrderId}. Status: {result.Status}";
}
```

---

## Provider Router Pattern

```csharp
// Provider selection priority:
// 1. Per-request override (query.ProviderOverride)
// 2. User preference primary provider (from PostgreSQL user_prefs)
// 3. Walk fallback chain in order until one is available
// 4. Local provider (Ollama) — always available as final fallback

// Provider MUST implement CheckAvailabilityAsync with < 500ms timeout
// Use Circuit Breaker pattern: mark provider as unavailable for 60s after 3 failures
```

---

## Context Assembly Rules

```csharp
// ContextAssembler MUST:
// 1. Query all 10 sources in PARALLEL (Task.WhenAll with individual timeouts)
// 2. Complete within 200ms total
// 3. Use partial context if any source times out (never throw, never block)
// 4. Never include sensitive data (private keys, API keys) in context

// Pattern:
var snapshot = new ProjectSnapshot();
await Task.WhenAll(
    FillModulesAsync(snapshot, ct).WithTimeout(100),
    FillPositionsAsync(snapshot, ct).WithTimeout(100),
    FillStrategiesAsync(snapshot, ct).WithTimeout(100),
    // ... other sources
);
```

---

## Canvas Action Dispatch

```csharp
// Canvas actions flow:
// Plugin function → CanvasActionDispatcher.DispatchAsync()
//   → Serialize to AI_CANVAS_ACTION envelope
//   → Block Controller routes to web-app
//   → web-app AICanvasService.HandleAsync()
//   → WindowManager.OpenPanel() / chart update / etc.

// Action types:
OpenPanelAction     → opens new DocumentWindow in MDI canvas
UpdateChartAction   → pushes new data series to existing chart
HighlightBlockAction → pulse animation on designer block node
ShowDiagramAction   → renders Mermaid diagram in new panel
AddAnnotationAction → adds marker on chart at timestamp
OpenDesignerGraphAction → loads strategy schema in DesignerCanvas
```

---

## Streaming Response Rules

```csharp
// Streaming: use SK InvokeStreamingAsync — never buffer full response
// Each chunk: emit AI_RESPONSE_CHUNK via SignalR
// Canvas actions: dispatched when SK resolves function call (before final text)
// Always emit AI_RESPONSE_COMPLETE as final message

await foreach (var chunk in _kernel.InvokeStreamingAsync(function, arguments, ct))
{
    if (chunk is StreamingFunctionCallUpdateContent functionCall)
    {
        // Resolve and dispatch canvas action immediately
        await HandleFunctionCallAsync(functionCall, userId, ct);
    }
    else if (chunk is StreamingTextContent textChunk)
    {
        await _hub.SendChunkAsync(userId, textChunk.Text, ct);
    }
}
await _hub.SendCompleteAsync(userId, ct);
```

---

## System Prompt Construction

```csharp
// Build system prompt from ProjectSnapshot
// Include: module health, active strategy, positions summary, available tools
// Keep under 2000 tokens to leave room for conversation + tool results
// Never include: raw DB queries, full envelope history, private keys

var systemPrompt = $"""
You are the MLS Platform AI Assistant with full awareness of the current system state.

## Platform Status
{FormatModuleHealth(snapshot.Modules)}

## Active Strategy
{snapshot.ActiveStrategy?.Name ?? "No strategy deployed"}

## Open Positions
{FormatPositions(snapshot.Positions)}

## Rules
- Always use tools for live data — don't guess
- Canvas actions happen automatically via your tool calls
- For trading actions, require confirmation before PlaceOrder
""";
```
