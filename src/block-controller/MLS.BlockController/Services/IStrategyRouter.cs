using MLS.Core.Contracts.Designer;

namespace MLS.BlockController.Services;

/// <summary>
/// Parses and deploys strategy graphs from <c>STRATEGY_DEPLOY</c> envelopes,
/// translating block-graph connections into subscription table entries so that
/// live <c>BLOCK_SIGNAL</c> envelopes are routed according to designer connections.
/// </summary>
public interface IStrategyRouter
{
    /// <summary>
    /// Deploy a strategy graph: validate, clear previous subscriptions, register
    /// new topic routes, and broadcast <c>STRATEGY_STATE_CHANGE(Running)</c>.
    /// </summary>
    public Task DeployAsync(StrategyGraphPayload graph, CancellationToken ct = default);

    /// <summary>
    /// Stop a running strategy: clear its subscriptions and broadcast
    /// <c>STRATEGY_STATE_CHANGE(Stopped)</c>.
    /// </summary>
    public Task StopAsync(Guid strategyId, CancellationToken ct = default);

    /// <summary>
    /// Begin backtest mode for a strategy: broadcast
    /// <c>STRATEGY_STATE_CHANGE(Backtesting)</c>.
    /// </summary>
    public Task BacktestAsync(Guid strategyId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
