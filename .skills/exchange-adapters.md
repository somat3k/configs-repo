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

---

## IExchangeAdapter Interface

```csharp
/// <summary>Normalised exchange adapter interface — all DEX/CEX integrations implement this.</summary>
public interface IExchangeAdapter : IAsyncDisposable
{
    /// <summary>Unique exchange key, e.g. <c>"camelot"</c>, <c>"hyperliquid"</c>.</summary>
    string ExchangeId { get; }

    /// <summary>Get current price for a token pair (< 100ms via Redis cache).</summary>
    Task<decimal> GetPriceAsync(string baseToken, string quoteToken, CancellationToken ct);

    /// <summary>Execute a swap. ALWAYS validate slippage before submitting.</summary>
    Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct);

    /// <summary>Get order book depth snapshot for slippage estimation.</summary>
    Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct);

    /// <summary>Subscribe to a live price stream. Yields updates until ct is cancelled.</summary>
    IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(string symbol, CancellationToken ct);

    /// <summary>Check liveness. Returns false if the exchange is unreachable.</summary>
    Task<bool> CheckAvailabilityAsync(CancellationToken ct);
}
```

---

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

// Gas estimation before swap
var gasEstimate = await contract.GetFunction("swapExactTokensForTokens")
    .EstimateGasAsync(from: walletAddress, value: null,
        amountIn, amountOutMin, path, to, deadline);
```

## Slippage Validation Pattern

```csharp
public async Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct)
{
    // 1. Get current price
    var currentPrice = await GetPriceAsync(request.TokenIn, request.TokenOut, ct);

    // 2. Calculate expected output
    var expectedOut = request.AmountIn * currentPrice;

    // 3. Apply slippage tolerance (e.g. 0.5%)
    var minOut = expectedOut * (1 - request.SlippageTolerance);

    // 4. REJECT if market has moved beyond tolerance since request was created
    if (request.ExpectedAmountOut > 0
        && expectedOut < request.ExpectedAmountOut * (1 - request.SlippageTolerance))
    {
        throw new SlippageExceededException(request, expectedOut, request.ExpectedAmountOut);
    }

    // 5. Submit with minAmountOut to prevent sandwich attacks
    return await SubmitOnChainSwapAsync(request, minOut, ct);
}
```

---

## Hyperliquid Adapter

```csharp
// Hyperliquid is primary DEX/perpetuals broker — REST + WebSocket
// REST base: https://api.hyperliquid.xyz
// WebSocket: wss://api.hyperliquid.xyz/ws

// Subscribe to candle stream
var msg = new { method = "subscribe", subscription = new { type = "candle", coin = "BTC", interval = "5m" } };
await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(msg), ct);

// Subscribe to L2 order book
var msg = new { method = "subscribe", subscription = new { type = "l2Book", coin = "BTC" } };

// Order placement (signed with EIP-712)
var order = new HyperLiquidOrder
{
    Coin     = "BTC",
    IsBuy    = true,
    Sz       = 0.001m,
    LimitPx  = 65000m,
    OrderType = new LimitOrderType { Tif = "Gtc" },
    ReduceOnly = false
};
var signedAction = await _wallet.SignOrderAsync(order, nonce, ct);
await _httpClient.PostAsJsonAsync("/exchange", signedAction, ct);
```

## Camelot (Arbitrum) Adapter

```csharp
// Camelot V2 — UniswapV2-compatible but with dual fee (stable/volatile pools)
// Router: loaded from BlockchainAddress.CamelotRouterV2 via IBlockchainAddressBook

// Get reserves from pair contract
var pairAddress = await _factory.GetFunction("getPair")
    .CallAsync<string>(token0, token1);

var (reserve0, reserve1, _) = await pair.GetFunction("getReserves")
    .CallDeserializingToObjectAsync<GetReservesOutputDTO>();

// Price = reserve1 / reserve0 (adjust for decimals)
var price = (decimal)reserve1 / (decimal)reserve0
          * (decimal)Math.Pow(10, token0Decimals - token1Decimals);
```

---

## nHOP Path Finder Key Rules

```
NO Uniswap: Only Camelot, DFYN, Balancer, Hyperliquid as exchange nodes
Max depth: 4 hops (configurable, default 4)
Min profit threshold: configurable, default $10 USD after gas + fees
Graph edges: (tokenIn, tokenOut, exchange) → (price, liquidity, gasEstimate)
Algorithm: BFS on token graph + Bellman-Ford for negative cycles (arbitrage detection)
Slippage: estimated from order book depth at the target swap size
Flash loan feasibility: net profit MUST exceed flash loan fee (0.09%) + gas + slippage
Emit ARB_PATH_FOUND only if net profit > min_profit_usd threshold
```

## nHOP Graph Construction

```csharp
public sealed class TokenGraph
{
    // Edges: (tokenIn, tokenOut) → list of (exchange, price, fee, gasEstimate)
    private readonly ConcurrentDictionary<(string, string), List<GraphEdge>> _edges = new();

    // Called on each price update from feed collectors
    public void UpdateEdge(string tokenIn, string tokenOut, string exchange,
        decimal price, decimal fee, decimal gasUsd)
    {
        var key = (tokenIn, tokenOut);
        var edges = _edges.GetOrAdd(key, _ => new());

        var idx = edges.FindIndex(e => e.Exchange == exchange);
        var edge = new GraphEdge(exchange, price, fee, gasUsd);
        if (idx >= 0) edges[idx] = edge;
        else edges.Add(edge);
    }

    // BFS over max 4 hops — returns best path or null if no profitable path
    public ArbPath? FindBestPath(string startToken, decimal amountIn, decimal minProfitUsd)
    {
        // Implementation: BFS + Bellman-Ford, emit ARB_PATH_FOUND if profitable
    }
}
```

---

## Exponential Backoff Pattern

```csharp
// Applied by ALL adapters on connection failure
private static readonly TimeSpan[] BackoffDelays =
    Enumerable.Range(0, 10)
        .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
        .ToArray();

private static TimeSpan GetBackoff(int attempt)
{
    var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
    var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
    return baseDelay + jitter;
}
```

---

## Testing Requirements

```csharp
// ALL exchange adapters MUST have:
// 1. Unit tests with mocked HTTP/WS responses (no live exchange calls in CI)
// 2. SlippageExceededException test for borderline tolerance case
// 3. Price caching test: second GetPriceAsync within TTL must NOT hit exchange
// 4. Backoff test: 3 consecutive failures → correct delay sequence

[Fact]
public async Task CamelotAdapter_GetPrice_UsesCacheOnSecondCall()
{
    _mockHttp.Setup(...).Returns(Task.FromResult(65000m)).Verifiable();
    var adapter = new CamelotAdapter(_mockHttp.Object, _cache);

    await adapter.GetPriceAsync("WBTC", "USDC", ct);
    await adapter.GetPriceAsync("WBTC", "USDC", ct); // Should hit cache

    _mockHttp.Verify(m => m.GetPriceAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
}
```
