---
rule: no-uniswap
applies-to: "**/*.cs,**/*.py,**/*.ts"
severity: error
---

# No Uniswap Integrations

**STRICT PROHIBITION**: The MLS platform does NOT use Uniswap in any form.

## Forbidden
- `Uniswap` — any class, interface, or service referencing Uniswap
- `UniswapV2`, `UniswapV3`, `UniswapV4` — any version
- `uniswap-sdk`, `@uniswap/*` — any npm packages
- `IUniswap*` — any Uniswap interface
- Any contract address known to be Uniswap Router or Factory

## Required Alternative
Use **HYPERLIQUID** as the primary DEX/perpetuals broker:
- REST API: `IHyperliquidClient`
- WebSocket: `IHyperliquidFeedClient`
- Fallback chain: `IBrokerFallbackChain` → Broker1 → Broker2

## Reference
- [.skills/web3.md](../../.skills/web3.md)
- [src/modules/defi/docs/SESSION.md](../../src/modules/defi/docs/SESSION.md)
