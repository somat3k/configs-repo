using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MLS.DataLayer.Persistence;

/// <summary>
/// Represents a single OHLCV candle stored in the <c>candles</c> PostgreSQL table.
/// </summary>
[Table("candles")]
public sealed class CandleEntity
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Exchange identifier, e.g. <c>hyperliquid</c>, <c>camelot</c>.</summary>
    [Required]
    [MaxLength(64)]
    [Column("exchange")]
    public string Exchange { get; set; } = string.Empty;

    /// <summary>Normalised trading symbol, e.g. <c>BTC-USDT</c>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Candle timeframe, e.g. <c>1m</c>, <c>5m</c>, <c>1h</c>, <c>1d</c>.</summary>
    [Required]
    [MaxLength(8)]
    [Column("timeframe")]
    public string Timeframe { get; set; } = string.Empty;

    /// <summary>Candle open timestamp (UTC).</summary>
    [Column("open_time")]
    public DateTimeOffset OpenTime { get; set; }

    /// <summary>Open price.</summary>
    [Column("open")]
    public double Open { get; set; }

    /// <summary>High price.</summary>
    [Column("high")]
    public double High { get; set; }

    /// <summary>Low price.</summary>
    [Column("low")]
    public double Low { get; set; }

    /// <summary>Close price.</summary>
    [Column("close")]
    public double Close { get; set; }

    /// <summary>Base asset volume.</summary>
    [Column("volume")]
    public double Volume { get; set; }

    /// <summary>Quote asset volume (volume × close).</summary>
    [Column("quote_volume")]
    public double QuoteVolume { get; set; }

    /// <summary>Row insert timestamp (UTC).</summary>
    [Column("inserted_at")]
    public DateTimeOffset InsertedAt { get; set; } = DateTimeOffset.UtcNow;
}
