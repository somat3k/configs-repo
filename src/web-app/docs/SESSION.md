# Web App Module — Session Prompt

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 1. Module Identity

| Field | Value |
|---|---|
| **Name** | `web-app` |
| **Namespace** | `MLS.WebApp` |
| **Role** | Blazor Interactive Server + WASM dashboard — trading UI, module observatory, execution console |
| **HTTP Port** | `5200` |
| **WebSocket Port** | `6200` (SignalR hub for real-time UI updates) |
| **Container** | `mls-web-app` |
| **Docker image** | `ghcr.io/somat3k/mls-web-app:latest` |

---

## 2. Technology Stack

| Technology | Version | Purpose |
|---|---|---|
| **Blazor** | .NET 9 Interactive Server + WASM | Primary UI rendering mode |
| **Microsoft FluentUI Blazor** | Latest | Design system and components |
| **SignalR** | .NET 9 | Real-time module data feeds |
| **Chart.js / ApexCharts** | Latest | Trading charts and metrics |
| **xterm.js** | Latest | Embedded terminal for Shell VM console |

---

## 3. Key Pages & Components

```
Pages/
├── Dashboard.razor          # Module health observatory
├── Trading.razor            # Trader signals + order book
├── Arbitrage.razor          # Arbitrage opportunity scanner
├── DeFi.razor               # DeFi positions and strategies
├── MLRuntime.razor          # Model registry and inference metrics
├── DataLayer.razor          # Market data feed status
├── ExecutionConsole.razor   # Shell VM terminal (xterm.js embedded)
└── Settings.razor           # Platform configuration

Components/
├── ModuleCard.razor         # Health card for each registered module
├── TradingChart.razor       # OHLCV + signal overlay chart
├── OrderBook.razor          # Real-time order book display
├── PositionTable.razor      # Open positions grid
├── ArbitrageScanner.razor   # Live opportunity table with confidence
├── MetricsPanel.razor       # CPU/memory/throughput gauges
├── TerminalBlock.razor      # xterm.js wrapper for Shell VM sessions
└── EnvelopeViewer.razor     # Debug: raw envelope message stream
```

---

## 4. Real-Time Data Architecture

```csharp
namespace MLS.WebApp.Services;

/// <summary>Manages SignalR connection to Block Controller for live module events.</summary>
public interface IBlockControllerHub
{
    Task ConnectAsync(CancellationToken ct);
    IAsyncEnumerable<ModuleStatusUpdate> GetModuleUpdatesAsync(CancellationToken ct);
    IAsyncEnumerable<EnvelopePayload> GetEnvelopeStreamAsync(string[] topics, CancellationToken ct);
}

/// <summary>Subscribes to Shell VM WebSocket for real-time terminal output.</summary>
public interface IShellVMClient
{
    Task<Guid> StartSessionAsync(string command, CancellationToken ct);
    IAsyncEnumerable<ShellOutputChunk> GetOutputStreamAsync(Guid sessionId, CancellationToken ct);
    ValueTask SendInputAsync(Guid sessionId, string input, CancellationToken ct);
    Task TerminateSessionAsync(Guid sessionId, CancellationToken ct);
}
```

---

## 5. Module Observatory

The Dashboard page aggregates health from all registered modules via Block Controller:

```csharp
// ModuleCard displays:
// - Module name, status (Healthy / Degraded / Offline)
// - Uptime seconds
// - CPU %, memory MB
// - Last heartbeat timestamp (relative: "2s ago")
// - Module-specific metrics (active sessions, orders processed, etc.)
```

---

## 6. Execution Console (Shell VM Integration)

The `ExecutionConsole.razor` page embeds an xterm.js terminal component (`TerminalBlock.razor`)
and connects to the shell-vm module at `ws://shell-vm:6950/ws/hub`:

```
User → TerminalBlock (xterm.js)
     → IShellVMClient.SendInputAsync(sessionId, "python train.py\n")
     → shell-vm WS (SHELL_INPUT envelope)

shell-vm → WS (SHELL_OUTPUT envelope)
         → IShellVMClient output stream
         → TerminalBlock.Write(chunk) → xterm.js renders
```

---

## 7. SignalR Hub (Web App Server-Side)

```csharp
namespace MLS.WebApp.Hubs;

/// <summary>
/// Web App's SignalR hub that proxies Block Controller events to browser clients.
/// Clients connect via JavaScript SignalR client in Blazor WASM.
/// </summary>
public sealed class DashboardHub(
    IBlockControllerHub _bc,
    ILogger<DashboardHub> _logger
) : Hub<IDashboardHubClient>
{
    public async Task SubscribeModules()    { /* join BC event stream */ }
    public async Task SubscribeTopic(string topic) { /* proxy BC envelope by type */ }
}

public interface IDashboardHubClient
{
    Task ReceiveModuleUpdate(ModuleStatusUpdate update);
    Task ReceiveEnvelope(EnvelopePayload envelope);
    Task ReceiveAlert(string message, AlertSeverity severity);
}
```

---

## 8. Configuration

```json
{
  "MLS": {
    "Module": "web-app",
    "HttpPort": 5200,
    "Network": {
      "BlockControllerUrl": "http://block-controller:5100",
      "BlockControllerWsUrl": "ws://block-controller:6100/ws/hub",
      "ShellVMUrl": "http://shell-vm:5950",
      "ShellVMWsUrl": "ws://shell-vm:6950/ws/hub"
    }
  }
}
```

---

## 9. Skills to Apply

- `.skills/premium-uiux-blazor.md` — FluentUI components, Blazor rendering modes, accessibility
- `.skills/web-apps.md` — Blazor WASM + Server hybrid, SignalR client, JS interop
- `.skills/websockets-inferences.md` — SignalR hub, real-time data proxy
- `.skills/networking.md` — Block Controller registration, service discovery
- `.skills/dotnet-devs.md` — C# 13, DI, async patterns
