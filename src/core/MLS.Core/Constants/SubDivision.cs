namespace MLS.Core.Constants;

/// <summary>
/// Named constants for all block sub-division routing keys used by the
/// <c>ITransformationController</c> to dispatch envelopes through named processing lanes.
/// </summary>
/// <remarks>
/// Always reference these constants instead of inline string literals so that sub-division
/// routing is refactor-safe and self-documenting.
/// </remarks>
public static class SubDivision
{
    /// <summary>Machine-learning feature engineering and inference pipeline.</summary>
    public const string Ml = "ml";

    /// <summary>Risk management pipeline (position sizing, stop-loss, drawdown guard).</summary>
    public const string Risk = "risk";

    /// <summary>Order execution pipeline (routing, slippage, fill tracking).</summary>
    public const string Execution = "execution";

    /// <summary>DeFi yield, lending, and collateral management pipeline.</summary>
    public const string DeFi = "defi";

    /// <summary>Cross-exchange arbitrage detection and path-finding pipeline.</summary>
    public const string Arbitrage = "arbitrage";

    /// <summary>Data ingestion and normalisation pipeline (Hydra domain).</summary>
    public const string Data = "data";

    /// <summary>Strategy-level signal generation and evaluation pipeline.</summary>
    public const string Strategy = "strategy";
}
