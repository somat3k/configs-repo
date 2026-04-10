namespace MLS.Trader.Persistence;

/// <summary>EF Core entity mapped to the <c>trader_positions</c> table.</summary>
public sealed class PositionEntity
{
    /// <summary>Primary key (auto-increment).</summary>
    public long Id { get; set; }

    /// <summary>Normalised trading symbol, e.g. <c>BTC-USDT</c>.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Position direction: <c>Buy</c> (long) or <c>Sell</c> (short).</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>Position size in base asset units.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Volume-weighted average entry price.</summary>
    public decimal AverageEntryPrice { get; set; }

    /// <summary>Mark-to-market PnL in quote currency.</summary>
    public decimal UnrealisedPnl { get; set; }

    /// <summary>Venue identifier, e.g. <c>hyperliquid</c>.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
