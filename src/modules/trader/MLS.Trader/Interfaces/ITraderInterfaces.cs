using System.Runtime.CompilerServices;
using MLS.Trader.Models;

namespace MLS.Trader.Interfaces;

/// <summary>
/// Generates trade signals from market feature data using the model-t ONNX model
/// or a rule-based fallback when the model is unavailable.
/// </summary>
public interface ISignalEngine
{
    /// <summary>
    /// Produces a <see cref="TradeSignalResult"/> for the given <paramref name="features"/>.
    /// When the ONNX model is loaded the inference runs in &lt; 10 ms;
    /// otherwise the rule-based scorer is used.
    /// </summary>
    ValueTask<TradeSignalResult> GenerateSignalAsync(MarketFeatures features, CancellationToken ct);
}

/// <summary>
/// Computes risk parameters for a given signal: position size via Kelly Criterion,
/// stop-loss via ATR or fixed percentage, and take-profit via the configured R:R ratio.
/// </summary>
public interface IRiskManager
{
    /// <summary>
    /// Computes the Kelly-adjusted position size in USD.
    /// The result is capped at <c>TraderOptions.MaxPositionSizeUsd</c>.
    /// Returns zero when the Kelly fraction is non-positive.
    /// </summary>
    /// <param name="confidence">Model confidence in [0, 1].</param>
    /// <param name="riskRewardRatio">Reward divided by risk (default 2:1).</param>
    decimal ComputePositionSize(float confidence, double riskRewardRatio);

    /// <summary>
    /// Computes the stop-loss price for the given direction.
    /// ATR-based when <paramref name="atr"/> &gt; 0; falls back to a fixed percentage otherwise.
    /// </summary>
    /// <param name="entryPrice">Entry price at signal time.</param>
    /// <param name="direction">BUY or SELL.</param>
    /// <param name="atr">Average True Range value; 0 triggers the fixed-percentage fallback.</param>
    decimal ComputeStopLoss(decimal entryPrice, SignalDirection direction, float atr);

    /// <summary>
    /// Computes the take-profit price given the stop-loss distance and R:R ratio.
    /// </summary>
    /// <param name="entryPrice">Entry price at signal time.</param>
    /// <param name="stopLossPrice">Computed stop-loss price.</param>
    /// <param name="direction">BUY or SELL.</param>
    decimal ComputeTakeProfit(decimal entryPrice, decimal stopLossPrice, SignalDirection direction);
}

/// <summary>
/// Manages the lifecycle of trader-owned orders.
/// Persists orders to PostgreSQL and maintains an in-memory state cache.
/// </summary>
public interface IOrderManager
{
    /// <summary>
    /// Creates a new order in <see cref="TraderOrderState.Draft"/> state.
    /// In paper-trading mode the order is immediately transitioned to
    /// <see cref="TraderOrderState.Filled"/> without contacting the Broker.
    /// In live mode an <c>ORDER_CREATE</c> envelope is dispatched and the state
    /// transitions to <see cref="TraderOrderState.Pending"/>.
    /// </summary>
    Task<TraderOrder> CreateOrderAsync(
        string          symbol,
        SignalDirection direction,
        decimal         quantity,
        decimal         entryPrice,
        decimal         stopLossPrice,
        decimal         takeProfitPrice,
        bool            paperTrading,
        CancellationToken ct);

    /// <summary>
    /// Cancels an open or pending order identified by <paramref name="clientOrderId"/>.
    /// In live mode sends an <c>ORDER_CANCEL</c> envelope to the Broker.
    /// </summary>
    Task CancelOrderAsync(string clientOrderId, CancellationToken ct);

    /// <summary>
    /// Transitions the state of an existing order.
    /// Updates both the in-memory cache and the PostgreSQL record.
    /// </summary>
    Task UpdateOrderStateAsync(string clientOrderId, TraderOrderState newState, CancellationToken ct);

    /// <summary>
    /// Returns the <see cref="TraderOrder"/> for the given <paramref name="clientOrderId"/>,
    /// or <see langword="null"/> when not found.
    /// </summary>
    Task<TraderOrder?> GetOrderAsync(string clientOrderId, CancellationToken ct);

    /// <summary>
    /// Streams all orders in an open or partially-filled state.
    /// </summary>
    IAsyncEnumerable<TraderOrder> GetOpenOrdersAsync(CancellationToken ct);
}
