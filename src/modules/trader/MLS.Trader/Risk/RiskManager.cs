using MLS.Trader.Configuration;
using MLS.Trader.Interfaces;
using MLS.Trader.Models;

namespace MLS.Trader.Risk;

/// <summary>
/// Computes risk parameters for each trade signal using the Kelly Criterion for position sizing,
/// ATR-based (or fixed-percentage) stop-loss calculation, and a configurable R:R take-profit.
/// </summary>
public sealed class RiskManager(
    IOptions<TraderOptions> _options,
    ILogger<RiskManager> _logger) : IRiskManager
{
    /// <inheritdoc/>
    public decimal ComputePositionSize(float confidence, double riskRewardRatio)
    {
        // Kelly Criterion: f = p - q/b
        //   p = win probability (confidence)
        //   q = 1 - p (loss probability)
        //   b = reward-to-risk ratio
        //   f = fraction of equity to risk
        var p = (double)confidence;
        var q = 1.0 - p;
        var b = riskRewardRatio > 0 ? riskRewardRatio : _options.Value.RiskRewardRatio;

        var kellyFraction = p - q / b;

        if (kellyFraction <= 0.0)
        {
            _logger.LogDebug("RiskManager: Kelly fraction {F:F4} ≤ 0 — no position", kellyFraction);
            return 0m;
        }

        var equity    = _options.Value.AccountEquityUsd;
        var rawSize   = (decimal)kellyFraction * equity;
        var capped    = Math.Min(rawSize, _options.Value.MaxPositionSizeUsd);

        _logger.LogDebug(
            "RiskManager: confidence={Conf:F3} Kelly={F:F4} rawSize={Raw:F2} capped={Cap:F2}",
            confidence, kellyFraction, rawSize, capped);

        return capped;
    }

    /// <inheritdoc/>
    public decimal ComputeStopLoss(decimal entryPrice, SignalDirection direction, float atr)
    {
        decimal stopDistance;

        if (atr > 0f)
        {
            stopDistance = (decimal)((double)atr * _options.Value.AtrMultiplier);
        }
        else
        {
            // Fixed-percentage fallback
            stopDistance = entryPrice * (decimal)_options.Value.StopLossPercent;
        }

        return direction == SignalDirection.Buy
            ? entryPrice - stopDistance
            : entryPrice + stopDistance;
    }

    /// <inheritdoc/>
    public decimal ComputeTakeProfit(decimal entryPrice, decimal stopLossPrice, SignalDirection direction)
    {
        var stopDistance = Math.Abs(entryPrice - stopLossPrice);
        var tpDistance   = stopDistance * (decimal)_options.Value.RiskRewardRatio;

        return direction == SignalDirection.Buy
            ? entryPrice + tpDistance
            : entryPrice - tpDistance;
    }
}
