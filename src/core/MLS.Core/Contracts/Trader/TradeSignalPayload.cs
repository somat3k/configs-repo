using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Trader;

/// <summary>
/// Payload for <c>TRADE_SIGNAL</c> envelopes emitted by the Trader module.
/// </summary>
/// <param name="Symbol">Normalised trading symbol, e.g. <c>BTC-USDT</c>.</param>
/// <param name="Direction">Signal direction: <c>Buy</c>, <c>Sell</c>, or <c>Hold</c>.</param>
/// <param name="Confidence">Model confidence in [0, 1]; higher values favour execution.</param>
/// <param name="PositionSizeUsd">Risk-adjusted notional position size in USD.</param>
/// <param name="EntryPrice">Suggested entry price at signal time.</param>
/// <param name="StopLossPrice">Computed stop-loss price (ATR-based or fixed percentage).</param>
/// <param name="TakeProfitPrice">Computed take-profit price at the configured R:R ratio.</param>
/// <param name="PaperTrading">When <see langword="true"/> the signal was not submitted to the broker.</param>
/// <param name="ClientOrderId">UUID correlating this signal with the order sent to the broker.</param>
/// <param name="Timestamp">UTC timestamp when the signal was generated.</param>
public sealed record TradeSignalPayload(
    [property: JsonPropertyName("symbol")]            string         Symbol,
    [property: JsonPropertyName("direction")]         string         Direction,
    [property: JsonPropertyName("confidence")]        float          Confidence,
    [property: JsonPropertyName("position_size_usd")] decimal        PositionSizeUsd,
    [property: JsonPropertyName("entry_price")]       decimal        EntryPrice,
    [property: JsonPropertyName("stop_loss_price")]   decimal        StopLossPrice,
    [property: JsonPropertyName("take_profit_price")] decimal        TakeProfitPrice,
    [property: JsonPropertyName("paper_trading")]     bool           PaperTrading,
    [property: JsonPropertyName("client_order_id")]   string         ClientOrderId,
    [property: JsonPropertyName("timestamp")]         DateTimeOffset Timestamp);
