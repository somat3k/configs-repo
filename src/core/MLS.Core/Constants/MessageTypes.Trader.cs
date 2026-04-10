namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Market data ───────────────────────────────────────────────────────────────
    /// <summary>data-layer or feed → trader: OHLCV + technical indicator snapshot for a symbol.</summary>
    public const string MarketDataUpdate = "MARKET_DATA_UPDATE";
}
