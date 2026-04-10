using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MLS.DeFi.Models;

namespace MLS.DeFi.Persistence;

/// <summary>
/// Persisted on-chain or venue-based transaction record in the <c>defi_transactions</c> table.
/// </summary>
[Table("defi_transactions")]
public sealed class TransactionEntity
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>Caller-generated UUID idempotency key.</summary>
    [Required]
    [MaxLength(64)]
    [Column("client_order_id")]
    public string ClientOrderId { get; set; } = string.Empty;

    /// <summary>Exchange-assigned or on-chain transaction hash; <see langword="null"/> until confirmed.</summary>
    [MaxLength(128)]
    [Column("venue_or_tx_id")]
    public string? VenueOrTxId { get; set; }

    /// <summary>Normalised trading symbol.</summary>
    [Required]
    [MaxLength(32)]
    [Column("symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Buy or Sell.</summary>
    [Required]
    [MaxLength(8)]
    [Column("side")]
    public string Side { get; set; } = string.Empty;

    /// <summary>Order type string.</summary>
    [Required]
    [MaxLength(16)]
    [Column("order_type")]
    public string OrderType { get; set; } = string.Empty;

    /// <summary>Requested quantity in base asset.</summary>
    [Column("quantity")]
    public decimal Quantity { get; set; }

    /// <summary>Limit price; <see langword="null"/> for market orders.</summary>
    [Column("limit_price")]
    public decimal? LimitPrice { get; set; }

    /// <summary>Current lifecycle state.</summary>
    [Required]
    [MaxLength(24)]
    [Column("state")]
    public string State { get; set; } = DeFiOrderState.Pending.ToString();

    /// <summary>Cumulative filled quantity.</summary>
    [Column("filled_quantity")]
    public decimal FilledQuantity { get; set; }

    /// <summary>Volume-weighted average fill price.</summary>
    [Column("average_price")]
    public decimal? AveragePrice { get; set; }

    /// <summary>Venue identifier (e.g. <c>hyperliquid</c>, <c>camelot</c>).</summary>
    [Required]
    [MaxLength(64)]
    [Column("venue")]
    public string Venue { get; set; } = string.Empty;

    /// <summary>Module ID of the requesting module.</summary>
    [Required]
    [MaxLength(64)]
    [Column("requesting_module_id")]
    public string RequestingModuleId { get; set; } = string.Empty;

    /// <summary>UTC creation timestamp.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of last state update.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
