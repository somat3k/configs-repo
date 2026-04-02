# defi — Session 3: Required Components

## Required Components

- `IHyperliquidClient` — HYPERLIQUID REST + WebSocket integration
- `IBrokerFallbackChain` — cascading fallback to configured brokers
- `IWalletProvider` — pluggable wallet backend (HSM/vault)
- `IOnChainTransactionService` — broadcast to blockchain
- `IDeFiStrategyEngine` — strategy selection and execution
