namespace MLS.Trader.Models;

// ── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>Direction of a generated trade signal.</summary>
public enum SignalDirection
{
    /// <summary>No trade action — hold the current position.</summary>
    Hold,
    /// <summary>Open or add to a long position.</summary>
    Buy,
    /// <summary>Open or add to a short position.</summary>
    Sell,
}

/// <summary>Lifecycle state of a trader-managed order.</summary>
public enum TraderOrderState
{
    /// <summary>Order created locally but not yet submitted to the broker.</summary>
    Draft,
    /// <summary>ORDER_CREATE envelope sent; awaiting broker acknowledgement.</summary>
    Pending,
    /// <summary>Order accepted and resting on the venue.</summary>
    Open,
    /// <summary>Order partially executed; remainder resting.</summary>
    PartiallyFilled,
    /// <summary>Order fully executed.</summary>
    Filled,
    /// <summary>Order cancelled by the trader or the venue.</summary>
    Cancelled,
}

// ── Value types ───────────────────────────────────────────────────────────────

/// <summary>
/// All technical indicator features required to produce a trade signal.
/// </summary>
/// <param name="Symbol">Normalised trading symbol, e.g. <c>BTC-USDT</c>.</param>
/// <param name="Price">Current mid-market price in quote currency.</param>
/// <param name="Rsi">RSI 14-period value in [0, 100].</param>
/// <param name="MacdValue">MACD line value.</param>
/// <param name="MacdSignal">MACD signal line value.</param>
/// <param name="BollingerUpper">Upper Bollinger Band (20-period SMA + 2σ).</param>
/// <param name="BollingerMiddle">Middle Bollinger Band (20-period SMA).</param>
/// <param name="BollingerLower">Lower Bollinger Band (20-period SMA − 2σ).</param>
/// <param name="VolumeDelta">Buy-side minus sell-side volume.</param>
/// <param name="Momentum">Rate of price change over the lookback period.</param>
/// <param name="AtrValue">Average True Range (14-period).</param>
/// <param name="Timestamp">UTC timestamp of this snapshot.</param>
public sealed record MarketFeatures(
    string         Symbol,
    decimal        Price,
    float          Rsi,
    float          MacdValue,
    float          MacdSignal,
    float          BollingerUpper,
    float          BollingerMiddle,
    float          BollingerLower,
    float          VolumeDelta,
    float          Momentum,
    float          AtrValue,
    DateTimeOffset Timestamp);

/// <summary>
/// A trade signal produced by the <see cref="MLS.Trader.Signals.SignalEngine"/>.
/// </summary>
/// <param name="Symbol">Normalised trading symbol.</param>
/// <param name="Direction">BUY, SELL, or HOLD.</param>
/// <param name="Confidence">Model confidence in [0, 1].</param>
/// <param name="Timestamp">UTC timestamp when the signal was generated.</param>
public sealed record TradeSignalResult(
    string         Symbol,
    SignalDirection Direction,
    float          Confidence,
    DateTimeOffset Timestamp);

/// <summary>
/// Risk parameters computed for a single signal.
/// </summary>
/// <param name="PositionSizeUsd">Kelly-adjusted position size in USD.</param>
/// <param name="StopLossPrice">Computed stop-loss price level.</param>
/// <param name="TakeProfitPrice">Computed take-profit price level.</param>
public sealed record RiskParameters(
    decimal PositionSizeUsd,
    decimal StopLossPrice,
    decimal TakeProfitPrice);

/// <summary>
/// Represents a single order managed by the Trader module.
/// </summary>
/// <param name="ClientOrderId">UUID generated at order creation time.</param>
/// <param name="Symbol">Normalised trading symbol.</param>
/// <param name="Direction">BUY or SELL direction.</param>
/// <param name="Quantity">Order size in base asset units.</param>
/// <param name="EntryPrice">Entry price at signal time.</param>
/// <param name="StopLossPrice">Computed stop-loss price.</param>
/// <param name="TakeProfitPrice">Computed take-profit price.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="PaperTrading">Whether this order was simulated locally.</param>
/// <param name="CreatedAt">UTC timestamp when the order was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent state change.</param>
public sealed record TraderOrder(
    string          ClientOrderId,
    string          Symbol,
    SignalDirection Direction,
    decimal         Quantity,
    decimal         EntryPrice,
    decimal         StopLossPrice,
    decimal         TakeProfitPrice,
    TraderOrderState State,
    bool            PaperTrading,
    DateTimeOffset  CreatedAt,
    DateTimeOffset  UpdatedAt);

/// <summary>
/// Open position snapshot maintained by the Trader module.
/// Updated on receipt of <c>POSITION_UPDATE</c> envelopes from the Broker.
/// </summary>
/// <param name="Symbol">Normalised trading symbol.</param>
/// <param name="Direction">Long (Buy) or Short (Sell).</param>
/// <param name="Quantity">Position size in base asset.</param>
/// <param name="AverageEntryPrice">Volume-weighted average entry price.</param>
/// <param name="UnrealisedPnl">Mark-to-market PnL in quote currency.</param>
/// <param name="Venue">Venue identifier, e.g. <c>hyperliquid</c>.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
public sealed record TraderPosition(
    string          Symbol,
    SignalDirection Direction,
    decimal         Quantity,
    decimal         AverageEntryPrice,
    decimal         UnrealisedPnl,
    string          Venue,
    DateTimeOffset  UpdatedAt);
