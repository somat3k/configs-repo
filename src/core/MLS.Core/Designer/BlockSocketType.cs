namespace MLS.Core.Designer;

/// <summary>
/// Strongly-typed socket data types for the MLS block graph.
/// Socket connections are only valid when both endpoints share the same <see cref="BlockSocketType"/>.
/// </summary>
public enum BlockSocketType
{
    // ── Data source outputs ──────────────────────────────────────────────────────
    /// <summary>OHLCV candle stream. Wire colour: blue.</summary>
    CandleStream,
    /// <summary>Level-2 order book update. Wire colour: dark blue.</summary>
    OrderBookUpdate,
    /// <summary>Tick-by-tick trade stream. Wire colour: navy.</summary>
    TradeTickStream,
    /// <summary>Arbitrum on-chain event log. Wire colour: indigo.</summary>
    OnChainEvent,

    // ── Indicator outputs ────────────────────────────────────────────────────────
    /// <summary>Single normalised indicator value (float, range varies). Wire colour: cyan.</summary>
    IndicatorValue,
    /// <summary>Rolling window of indicator values (float[]). Wire colour: light cyan.</summary>
    IndicatorSeries,

    // ── ML outputs ───────────────────────────────────────────────────────────────
    /// <summary>Model signal: direction + confidence. Wire colour: purple.</summary>
    MLSignal,
    /// <summary>Per-class probability map. Wire colour: light purple.</summary>
    MLProbabilities,

    // ── Risk outputs ─────────────────────────────────────────────────────────────
    /// <summary>Risk gate decision: allow/deny + sizing. Wire colour: orange.</summary>
    RiskDecision,
    /// <summary>Portfolio exposure report. Wire colour: amber.</summary>
    ExposureUpdate,

    // ── Trading outputs ──────────────────────────────────────────────────────────
    /// <summary>Order request to be routed to a broker. Wire colour: green.</summary>
    TradeOrder,
    /// <summary>Fill confirmation from broker. Wire colour: lime.</summary>
    OrderResult,

    // ── Arbitrage outputs ────────────────────────────────────────────────────────
    /// <summary>Arbitrage opportunity with spread and profit estimate. Wire colour: yellow.</summary>
    ArbitrageOpportunity,
    /// <summary>nHOP token path graph update. Wire colour: gold.</summary>
    PathUpdate,

    // ── DeFi outputs ─────────────────────────────────────────────────────────────
    /// <summary>DeFi yield move signal (protocol + action). Wire colour: teal.</summary>
    DeFiSignal,
    /// <summary>Streaming health factor update. Wire colour: mint.</summary>
    HealthFactorUpdate,

    // ── Training outputs ─────────────────────────────────────────────────────────
    /// <summary>Training job status / metrics. Wire colour: pink.</summary>
    TrainingStatus,
    /// <summary>Model input feature vector (float[]). Wire colour: rose.</summary>
    FeatureVector,

    // ── Analytics outputs ────────────────────────────────────────────────────────
    /// <summary>Chart update for MDI canvas panel. Wire colour: white.</summary>
    ChartData,
    /// <summary>P&amp;L or analytics report. Wire colour: light grey.</summary>
    ReportData,
}
