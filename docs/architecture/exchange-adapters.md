> ✅ **Status: Complete** — Implemented and verified in session 23 (workflow-demo).

# Arbitrum Exchange Adapters

> **Reference**: [Giga-Scale Plan](giga-scale-plan.md) | [Session Schedule](../session-schedule.md) (Sessions 05, 15)

---

## IExchangeAdapter Interface

The MLS equivalent of StockSharp's `IMessageAdapter`. One implementation per exchange/protocol.

```csharp
/// <summary>Unified interface for all Arbitrum DEX and protocol adapters.</summary>
public interface IExchangeAdapter : IAsyncDisposable
{
    string ExchangeId { get; }          // "hyperliquid", "camelot", "dfyn", "balancer", "morpho"
    string DisplayName { get; }
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);

    /// Real-time spot/mark price for a symbol
    Task<decimal> GetPriceAsync(string symbol, CancellationToken ct);

    /// L2 order book snapshot
    Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct);

    /// Historical OHLCV candles (for Hydra backfill)
    IAsyncEnumerable<OHLCVCandle> GetHistoricalCandlesAsync(
        string symbol, string timeframe, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    /// Real-time price stream (for Hydra live collection)
    IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(string symbol, CancellationToken ct);

    /// Execute a swap or trade (returns immediately with transaction hash)
    Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct);

    /// Estimate gas cost for a swap
    Task<GasEstimate> EstimateGasAsync(SwapRequest request, CancellationToken ct);
}
```

---

## Exchange Adapter Implementations

### HyperliquidAdapter (Primary — Perpetuals)

**Role**: Primary DEX/perpetuals broker. Used by Trader, Arbitrager, and Broker modules.

```csharp
/// <summary>
/// HYPERLIQUID L1 adapter via official REST + WebSocket API.
/// Primary exchange for perpetual futures: BTC-PERP, ETH-PERP, ARB-PERP.
/// </summary>
public sealed class HyperliquidAdapter(
    HttpClient _http,
    IWebSocketClient _ws,
    IBlockchainAddressBook _addresses,
    ILogger<HyperliquidAdapter> _logger
) : IExchangeAdapter
{
    // REST: https://api.hyperliquid.xyz/info
    // WS:   wss://api.hyperliquid.xyz/ws
    //
    // WebSocket subscriptions:
    //   { "method": "subscribe", "subscription": { "type": "l2Book", "coin": "BTC" } }
    //   { "method": "subscribe", "subscription": { "type": "candle", "coin": "BTC", "interval": "5m" } }
    //   { "method": "subscribe", "subscription": { "type": "fills" } }
}
```

**Supported symbols**: BTC-PERP, ETH-PERP, ARB-PERP, SOL-PERP + all HYPERLIQUID listed perps

### CamelotAdapter (Arbitrum Native DEX)

**Role**: Primary Arbitrum DEX for spot arbitrage. Used in nHOP path finding.

```csharp
/// <summary>
/// Camelot DEX adapter on Arbitrum — AMM v2 + concentrated liquidity v3.
/// Uses Nethereum for on-chain reads. Camelot is the primary hop in nHOP arbitrage paths.
/// </summary>
public sealed class CamelotAdapter(
    IWeb3 _web3,                              // Nethereum Web3
    IBlockchainAddressBook _addresses,
    ILogger<CamelotAdapter> _logger
) : IExchangeAdapter
{
    // Router address: loaded from PostgreSQL via IBlockchainAddressBook
    // Pool discovery: factory.getPair(tokenA, tokenB)
    // Price reads: pair.getReserves() → constant product formula
    // Swaps: router.swapExactTokensForTokens()
}
```

**Key Pools**: WETH/USDC, WETH/ARB, WETH/WBTC, ARB/USDC, GMX/WETH

### DFYNAdapter

**Role**: Secondary hop in nHOP paths. Cross-chain DEX with Arbitrum bridge.

```csharp
public sealed class DFYNAdapter(
    HttpClient _restClient,                    // REST API for price queries
    IWeb3 _web3,                              // On-chain execution
    IBlockchainAddressBook _addresses,
    ILogger<DFYNAdapter> _logger
) : IExchangeAdapter
{
    // REST API: https://api.dfyn.network (price, liquidity)
    // On-chain execution via Nethereum DFYN router ABI
}
```

### BalancerAdapter (AMM + Supply/Borrow)

**Role**: AMM pool execution, collateral swaps, supply/borrow mechanics for DeFi module.

```csharp
/// <summary>
/// Balancer V2 Vault adapter on Arbitrum.
/// Supports: weighted pool swaps, stable pool swaps, batch swaps for multi-hop.
/// Also used for: Supply/Borrow/CollateralSwap/Repay mechanics in DeFi module.
/// </summary>
public sealed class BalancerAdapter(
    IWeb3 _web3,
    IBlockchainAddressBook _addresses,
    ILogger<BalancerAdapter> _logger
) : IExchangeAdapter
{
    // Vault address: loaded from PostgreSQL via IBlockchainAddressBook
    // Pool discovery: Vault.getPools() + getPoolTokens(poolId)
    // Swap: Vault.swap(SingleSwap, FundManagement, limit, deadline)
    // BatchSwap: Vault.batchSwap(SwapKind, BatchSwapStep[], assets, funds, limits, deadline)
}
```

### MorphoAdapter (Lending Protocol)

**Role**: Supply, Borrow, Repay, Liquidation monitoring for DeFi module.

```csharp
/// <summary>
/// Morpho lending protocol adapter on Arbitrum.
/// Handles: supply, borrow, repay, withdraw, health factor streaming.
/// </summary>
public sealed class MorphoAdapter(
    HttpClient _apiClient,                     // Morpho REST API for rates, utilization
    IWeb3 _web3,                              // On-chain execution (supply/borrow/repay)
    IBlockchainAddressBook _addresses,
    ILogger<MorphoAdapter> _logger
) : IExchangeAdapter
{
    // REST: https://api.morpho.org/rates/{market}
    // On-chain: MorphoBlue.supply(), .borrow(), .repay(), .withdraw()
    // Health factor: (collateral_value * liq_threshold) / borrow_value
}
```

---

## ExchangeRegistry — Address Book

All blockchain addresses stored in PostgreSQL. Never hardcoded.

```csharp
/// <summary>
/// PostgreSQL-backed registry of all blockchain contract addresses.
/// Loaded at startup via IBlockchainAddressBook and refreshed on REGISTER_UPDATE envelope.
/// </summary>
public interface IBlockchainAddressBook
{
    Task<string> GetAddressAsync(BlockchainAddress addressKey, CancellationToken ct);
    Task<IReadOnlyDictionary<BlockchainAddress, string>> GetAllAsync(CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
}

public enum BlockchainAddress
{
    // HYPERLIQUID (L1 chain)
    HyperliquidRouter,
    HyperliquidClearingHouse,

    // Camelot (Arbitrum)
    CamelotRouterV2,
    CamelotRouterV3,
    CamelotFactory,

    // DFYN (Arbitrum)
    DFYNRouter,
    DFYNFactory,

    // Balancer (Arbitrum)
    BalancerVault,
    BalancerQueryHelper,

    // Morpho (Arbitrum)
    MorphoBlue,
    MorphoMorphoAaveV3,

    // Tokens (Arbitrum)
    WETH,
    USDC,
    USDT,
    ARB,
    WBTC,
    GMX,
    RDNT,
}
```

### PostgreSQL Schema

```sql
CREATE TABLE blockchain_addresses (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    address_key VARCHAR(100) NOT NULL UNIQUE,   -- BlockchainAddress enum name
    chain_id    INTEGER NOT NULL,               -- 42161 = Arbitrum One
    address     VARCHAR(42) NOT NULL,           -- 0x hex
    label       VARCHAR(200),
    is_active   BOOLEAN DEFAULT TRUE,
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);
```

---

## nHOP Path Finder — Graph Algorithm

```
Token Graph (Arbitrum):
  Nodes: WETH, USDC, USDT, ARB, WBTC, GMX, RDNT
  Edges: (tokenIn, tokenOut, exchange, buy_price, sell_price, liquidity, gas_estimate_usd)

Example 3-hop arbitrage path:
  USDC →[Camelot]→ WETH →[DFYN]→ ARB →[Balancer]→ USDC
  Start: 10,000 USDC
  After hop 1: 10,000 / Camelot_WETH_price × (1 - Camelot_fee)
  After hop 2: [WETH amount] × DFYN_ARB_price × (1 - DFYN_fee)
  After hop 3: [ARB amount] × Balancer_USDC_price × (1 - Balancer_fee)
  Net profit = Final USDC - Initial USDC - gas_costs
```

```csharp
public sealed class nHOPPathFinder(
    IEnumerable<IExchangeAdapter> _adapters,
    IBlockchainAddressBook _addresses,
    ILogger<nHOPPathFinder> _logger
)
{
    private const int MaxHops = 4;
    private const decimal MinProfitUsd = 10m;

    public async IAsyncEnumerable<ArbOpportunity> FindPathsAsync(
        string[] tokens, [EnumeratorCancellation] CancellationToken ct)
    {
        // Build price graph from all adapters (parallel price queries)
        var graph = await BuildPriceGraphAsync(tokens, ct);

        // BFS over (token, exchange) node pairs, depth ≤ MaxHops
        foreach (var path in BreadthFirstSearch(graph, tokens))
        {
            var profit = CalculateProfit(path, graph);
            var gas = await EstimateTotalGasAsync(path, ct);

            if (profit - gas > MinProfitUsd)
                yield return new ArbOpportunity(path, profit, gas);
        }
    }
}
```

---

## No-Uniswap Rule

Per project constraints (`.github/copilot-rules/rule-no-uniswap.md`):

> **Never use Uniswap contracts, ABIs, SDKs, or APIs.**
> The supported exchanges for Arbitrum arbitrage are:
> - **HYPERLIQUID** (primary)
> - **Camelot DEX** (primary Arbitrum hop)
> - **DFYN** (secondary hop)
> - **Balancer** (AMM + lending mechanics)
> - **Morpho** (lending protocol)

---

## See Also

- [Session Schedule — Sessions 05, 15](../session-schedule.md#session-05--arbitrage--defi-domain-blocks--exchange-adapters)
- [Hydra Data Collection](hydra-data-collection.md) — how adapters feed the Hydra collectors
- [Designer Block Graph](designer-block-graph.md) — ArbitrageBlock + DeFiBlock types
- [Giga-Scale Plan](giga-scale-plan.md) — nHOP flow diagram
