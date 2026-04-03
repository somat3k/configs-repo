# defi — Session 3: Required Components

> Use this document as context when generating DeFi module code with GitHub Copilot.

## Required Components

- `IHyperliquidClient` — HYPERLIQUID REST + WebSocket integration
- `IBrokerFallbackChain` — cascading fallback to configured brokers
- `IWalletProvider` — pluggable wallet backend (HSM/vault)
- `IOnChainTransactionService` — broadcast to blockchain
- `IDeFiStrategyEngine` — strategy selection and execution
