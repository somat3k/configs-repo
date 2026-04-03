namespace MLS.Core.Constants;

/// <summary>
/// Enumeration of all known blockchain contract addresses used across the MLS platform.
/// Address values are loaded from PostgreSQL via <c>IBlockchainAddressBook</c> — never hardcoded.
/// </summary>
public enum BlockchainAddress
{
    // ── Camelot (Arbitrum native AMM) ─────────────────────────────────────────
    /// <summary>Camelot V2 router contract on Arbitrum.</summary>
    CamelotRouterV2,

    /// <summary>Camelot V2 factory contract on Arbitrum.</summary>
    CamelotFactoryV2,

    /// <summary>Camelot V3 (concentrated liquidity) NonfungiblePositionManager.</summary>
    CamelotPositionManagerV3,

    // ── DFYN (Arbitrum) ───────────────────────────────────────────────────────
    /// <summary>DFYN router contract on Arbitrum.</summary>
    DfynRouter,

    /// <summary>DFYN factory contract on Arbitrum.</summary>
    DfynFactory,

    // ── Balancer ──────────────────────────────────────────────────────────────
    /// <summary>Balancer Vault — entry point for all Balancer pool interactions.</summary>
    BalancerVault,

    /// <summary>Balancer weighted pool factory on Arbitrum.</summary>
    BalancerWeightedPoolFactory,

    /// <summary>Balancer queries helper contract.</summary>
    BalancerQueries,

    // ── Morpho Blue ───────────────────────────────────────────────────────────
    /// <summary>Morpho Blue core lending contract on Arbitrum.</summary>
    MorphoBlue,

    /// <summary>Morpho bundler (atomic multi-step transactions).</summary>
    MorphoBundler,

    /// <summary>Morpho adaptive curve interest rate model.</summary>
    MorphoAdaptiveCurveIrm,

    // ── WETH / Wrapped tokens ─────────────────────────────────────────────────
    /// <summary>Wrapped ETH (WETH) ERC-20 contract on Arbitrum.</summary>
    WethArbitrum,

    /// <summary>USDC ERC-20 contract on Arbitrum.</summary>
    UsdcArbitrum,

    /// <summary>ARB governance token ERC-20 contract on Arbitrum.</summary>
    ArbToken,

    /// <summary>Wrapped BTC (WBTC) ERC-20 contract on Arbitrum.</summary>
    WbtcArbitrum,

    /// <summary>GMX governance token ERC-20 contract on Arbitrum.</summary>
    GmxToken,

    /// <summary>Radiant Capital (RDNT) ERC-20 contract on Arbitrum.</summary>
    RdntToken,

    // ── Flash loan providers ──────────────────────────────────────────────────
    /// <summary>Aave-compatible flash loan provider on Arbitrum.</summary>
    FlashLoanProvider,

    // ── Multicall ─────────────────────────────────────────────────────────────
    /// <summary>Multicall3 contract on Arbitrum (batched read calls).</summary>
    Multicall3Arbitrum,
}
