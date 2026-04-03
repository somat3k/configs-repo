---
name: exchange-adapters
source: custom (MLS Trading Platform)
description: 'IExchangeAdapter pattern, Nethereum blockchain interactions, Arbitrum DEX specifics (Camelot, DFYN, Balancer, Morpho, Hyperliquid), nHOP arbitrage graph algorithm, and blockchain address book rules.'
---

# Exchange Adapters — MLS Trading Platform

> Apply this skill when implementing: `IExchangeAdapter` classes, on-chain reads/writes via Nethereum, nHOP path finding, or exchange feed collection.

---

## IExchangeAdapter Rules

```csharp
// 1. One class per exchange — no generic multi-exchange adapter
// 2. All blockchain addresses via IBlockchainAddressBook — NEVER hardcode
// 3. Implement exponential backoff on connection failure (base 1s, max 60s, jitter)
// 4. GetPriceAsync latency target: < 100ms (use Redis 1s TTL cache)
// 5. ExecuteSwapAsync: ALWAYS check slippage tolerance before submitting
// 6. All adapters MUST be IAsyncDisposable
```

## Blockchain Address Rules

```csharp
// ALWAYS use BlockchainAddress enum — never string literals
var routerAddress = await _addressBook.GetAddressAsync(BlockchainAddress.CamelotRouterV2, ct);

// NEVER:
var routerAddress = "0x1F98431c8aD98523631AE4a59f267346ea31F984";  // ❌ Hardcoded Uniswap!

// PostgreSQL table: blockchain_addresses (address_key, chain_id, address)
// Loaded at startup, refreshed on REGISTER_UPDATE envelope
```

## No-Uniswap Rule (Absolute)

```
❌ FORBIDDEN: Uniswap V2 Router, Uniswap V3 Router, UniswapV2Factory, UniswapV3Factory
❌ FORBIDDEN: @uniswap/sdk, @uniswap/v3-sdk, IUniswapV2Router, IUniswapV3Pool
✅ ALLOWED:   Camelot (Arbitrum native), DFYN, Balancer Vault, Morpho Blue, HYPERLIQUID
```

## Nethereum Patterns

```csharp
// Use Nethereum for all EVM chain interactions
// Web3 instance injected via DI — never instantiated inline

// Read (call, no gas)
var reserves = await pair.GetFunction("getReserves")
    .CallDeserializingToObjectAsync<GetReservesOutputDTO>(ct);

// Write (send transaction)
var txHash = await router.GetFunction("swapExactTokensForTokens")
    .SendTransactionAsync(from: walletAddress, gas: null, value: null,
        amountIn, amountOutMin, path, to, deadline);

// Event listening
var events = await web3.Eth.GetEvent<SwapEventDTO>(contractAddress)
    .GetAllChangesAsync(filter);
```

## Hyperliquid WebSocket Subscription

```csharp
// Subscribe to candle stream
var msg = new { method = "subscribe", subscription = new { type = "candle", coin = "BTC", interval = "5m" } };
await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(msg), ct);

// Subscribe to L2 order book
var msg = new { method = "subscribe", subscription = new { type = "l2Book", coin = "BTC" } };
```

## nHOP Path Finder Key Rules

```
- NEVER include Uniswap in token graph nodes
- Max depth: 4 hops (configurable)
- Score: (output - input - gas) / input  →  rank by ROI
- Slippage: estimate from order book depth at swap size
- Flash loan feasibility: profit must exceed flash loan fee + gas + slippage
- Emit ARB_PATH_FOUND only if net profit > min_profit_usd threshold (default $10)
```
