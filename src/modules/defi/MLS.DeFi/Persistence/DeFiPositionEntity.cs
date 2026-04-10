using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MLS.DeFi.Models;

namespace MLS.DeFi.Persistence;

/// <summary>
/// Persisted open position for a symbol on a venue, stored in the <c>defi_positions</c> table.
/// Each (symbol, venue) pair has at most one row.
/// </summary>
[Table("defi_positions")]
public sealed class DeFiPositionEntity
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Normalised trading symbol, e.g. <c>BTC-USDT</c>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Long or Short expressed as Buy/Sell.</summary>
    [Required]
    [MaxLength(8)]
    [Column("side")]
    public string Side { get; set; } = string.Empty;

    /// <summary>Current position size in base asset.</summary>
    [Column("quantity")]
    public decimal Quantity { get; set; }

    /// <summary>Volume-weighted average entry price.</summary>
    [Column("average_entry_price")]
    public decimal AverageEntryPrice { get; set; }

    /// <summary>Mark-to-market unrealised PnL in quote currency.</summary>
    [Column("unrealised_pnl")]
    public decimal UnrealisedPnl { get; set; }

    /// <summary>Venue identifier.</summary>
    [Required]
    [MaxLength(64)]
    [Column("venue")]
    public string Venue { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last position snapshot.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
