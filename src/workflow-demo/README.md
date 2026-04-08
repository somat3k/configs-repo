# MLS Workflow Demo

> Standalone, user-loadable ASP.NET Core + Blazor Server application.  
> One page per module — each fetches live data through a typed functional pipeline.

## Quick Start

```bash
dotnet run --project src/workflow-demo/MLS.WorkflowDemo
# → http://localhost:5099/workflow
```

No other services required. When the Hyperliquid and DeFi Llama APIs are reachable, pages display live data. When they are not (air-gapped CI, offline), a built-in market snapshot with realistic Hyperliquid perpetual parameters kicks in automatically.

## Routes

| URL | Module | Data Source |
|-----|--------|-------------|
| `/workflow` | Index | — |
| `/workflow/block-controller` | Block Controller | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/data-layer` | Data Layer | Hyperliquid `candleSnapshot` ETH 1m |
| `/workflow/trader` | Trader (model-t) | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/arbitrager` | Arbitrager (model-a) | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/defi` | DeFi (model-d) | DeFi Llama `/v2/protocols` |
| `/workflow/ml-runtime` | ML Runtime | Hyperliquid `candleSnapshot` ETH 1m |
| `/workflow/designer` | Designer | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/ai-hub` | AI Hub | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/broker` | Broker | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/transactions` | Transactions | Hyperliquid `metaAndAssetCtxs` |
| `/workflow/shell-vm` | Shell VM | Hyperliquid `metaAndAssetCtxs` |

## Architecture

```
WorkflowDataService          (pure functional fetch pipeline)
  ├── HlPost<T>()            (POST https://api.hyperliquid.xyz/info)
  ├── HttpGet<T>()           (GET  https://api.llama.fi/v2/protocols)
  ├── BuiltInMarkets()       (fallback: 20 real Hyperliquid perpetuals)
  ├── BuiltInCandles()       (fallback: deterministic OHLCV walk)
  ├── EngineerFeatures()     (8-dim RSI/MACD/BB/Vol/Mom/ATR/Spread/VWAP)
  └── ComputeArbOpportunities() (multi-venue spread detection)

WorkflowHeader.razor         (title, ports, live badge, data source)
WorkflowPipeline.razor       (horizontal step visualiser with active state)
Pages/
  ├── WorkflowIndex.razor    (@page "/workflow")
  ├── BlockControllerWorkflow.razor
  ├── DataLayerWorkflow.razor
  ├── TraderWorkflow.razor
  ├── ArbitragerWorkflow.razor
  ├── DefiWorkflow.razor
  ├── MlRuntimeWorkflow.razor
  ├── DesignerWorkflow.razor
  ├── AiHubWorkflow.razor
  ├── BrokerWorkflow.razor
  ├── TransactionsWorkflow.razor
  └── ShellVmWorkflow.razor
```

## Playwright Screenshots

```bash
# 1. Start the demo app (from repo root)
dotnet run --project src/workflow-demo/MLS.WorkflowDemo &

# 2. Install Playwright (also from repo root)
cd src/workflow-demo
npm init -y
npm install playwright
npx playwright install chromium
cd ../..

# 3. Run the screenshot script from the repo root
node src/workflow-demo/playwright-screenshot.js

# Screenshots saved to docs/screenshots/
```

## Key Design Decisions

- **Universal function-based**: all data transforms are pure functions (`Func<T>` composition, LINQ, static methods). No side-effects in the data pipeline.
- **Self-contained**: zero dependencies on other MLS modules. One `dotnet run` is all that's needed.
- **Demo pipeline**: `WorkflowDataService.EngineerFeatures()` demonstrates the same 8 features as `MLS.DataLayer.FeatureEngineer` using a simplified variant (14-candle minimum vs 34 in production).
- **Graceful fallback**: `SafeAsync` catches network failures and returns `BuiltIn*()` data so every page always renders.
- **Playwright sentinel**: every data-loading page renders `<div id="data-loaded">` when `OnInitializedAsync` completes, giving Playwright a reliable wait target.
