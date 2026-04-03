# defi — Session 2: Critical Rules

> Use this document as context when generating DeFi module code with GitHub Copilot.

## Critical Rules

1. **NEVER integrate Uniswap** — any mention should raise a build error
2. Primary broker: **HYPERLIQUID** (REST + WebSocket API)
3. Fallback chain: HYPERLIQUID → Broker1 → Broker2
4. All blockchain addresses from `blockchain_addresses` PostgreSQL table
