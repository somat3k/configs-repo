namespace MLS.Trader.Configuration;

/// <summary>
/// Configuration options for the Trader module, bound from <c>appsettings.json</c>
/// under the <c>"Trader"</c> section.
/// </summary>
public sealed class TraderOptions
{
    /// <summary>HTTP endpoint of this module, e.g. <c>http://trader:5300</c>.</summary>
    public string HttpEndpoint { get; set; } = "http://trader:5300";

    /// <summary>WebSocket endpoint of this module, e.g. <c>ws://trader:6300</c>.</summary>
    public string WsEndpoint { get; set; } = "ws://trader:6300";

    /// <summary>Block Controller HTTP base URL.</summary>
    public string BlockControllerUrl { get; set; } = "http://block-controller:5100";

    /// <summary>PostgreSQL connection string for trade and position storage.</summary>
    public string PostgresConnectionString { get; set; } =
        "Host=postgres;Port=5432;Database=mls_db;Username=mls_user;Password=mls";

    /// <summary>Redis connection string for position hot cache.</summary>
    public string RedisConnectionString { get; set; } = "redis:6379";

    /// <summary>
    /// Path to the model-t ONNX file.
    /// When empty or the file does not exist, the rule-based scorer is used as fallback.
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// When <see langword="true"/>, orders are simulated locally and never sent to the Broker.
    /// Defaults to <see langword="false"/> for live trading.
    /// </summary>
    public bool PaperTrading { get; set; } = false;

    /// <summary>
    /// Minimum model confidence in [0, 1] required to act on a BUY or SELL signal.
    /// Defaults to 0.65.
    /// </summary>
    public float MinSignalConfidence { get; set; } = 0.65f;

    /// <summary>
    /// Maximum position size in USD, regardless of Kelly Criterion output.
    /// Defaults to 10 000 USD.
    /// </summary>
    public decimal MaxPositionSizeUsd { get; set; } = 10_000m;

    /// <summary>
    /// Risk:Reward ratio for take-profit calculation.
    /// A value of 2.0 means take-profit is placed at 2× the stop-loss distance.
    /// Defaults to 2.0.
    /// </summary>
    public double RiskRewardRatio { get; set; } = 2.0;

    /// <summary>
    /// ATR multiplier for stop-loss distance.
    /// Stop-loss = entry price ± <see cref="AtrMultiplier"/> × ATR.
    /// Defaults to 2.0.
    /// </summary>
    public double AtrMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Fixed stop-loss percentage used when ATR is zero.
    /// Expressed as a fraction, e.g. 0.02 = 2 %.
    /// Defaults to 0.02.
    /// </summary>
    public double StopLossPercent { get; set; } = 0.02;

    /// <summary>
    /// Total account equity in USD used for Kelly Criterion position sizing.
    /// Defaults to 100 000 USD.
    /// </summary>
    public decimal AccountEquityUsd { get; set; } = 100_000m;

    /// <summary>
    /// Bounded capacity of the internal market-data processing channel.
    /// Defaults to 512.
    /// </summary>
    public int SignalChannelCapacity { get; set; } = 512;
}
