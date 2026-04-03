namespace MLS.BlockController.Constants;

/// <summary>Valid string values for <c>StrategyStateChangePayload.CurrentState</c>.</summary>
public static class StrategyState
{
    /// <summary>Strategy is actively processing live data.</summary>
    public const string Running     = "Running";
    /// <summary>Strategy is inactive and not processing data.</summary>
    public const string Stopped     = "Stopped";
    /// <summary>Strategy is running against historical data for backtest.</summary>
    public const string Backtesting = "Backtesting";
}
