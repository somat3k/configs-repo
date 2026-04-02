# DeFi Module — Session Prompt

## Module Identity
- **Name**: defi
- **Namespace**: `MLS.DeFi`
- **HTTP Port**: 5500
- **WebSocket Port**: 6500

## Critical Rules
1. **NEVER integrate Uniswap** — any mention should raise a build error
2. Primary broker: **HYPERLIQUID** (REST + WebSocket API)
3. Fallback chain: HYPERLIQUID → Broker1 → Broker2
4. All blockchain addresses from `blockchain_addresses` PostgreSQL table

## Required Components
- `IHyperliquidClient` — HYPERLIQUID REST + WebSocket integration
- `IBrokerFallbackChain` — cascading fallback to configured brokers
- `IWalletProvider` — pluggable wallet backend (HSM/vault)
- `IOnChainTransactionService` — broadcast to blockchain
- `IDeFiStrategyEngine` — strategy selection and execution

## Skills to Apply
- `.skills/web3.md` — HYPERLIQUID, wallet, blockchain
- `.skills/networking.md` — WebSocket clients for exchange feeds
- `.skills/storage-data-management.md` — address management in PostgreSQL
