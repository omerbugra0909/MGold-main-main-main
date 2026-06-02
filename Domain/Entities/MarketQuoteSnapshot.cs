using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Domain.Entities;

public class MarketQuoteSnapshot
{
    public int Id { get; set; }

    [Required]
    [MaxLength(40)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    public MarketCategory Category { get; set; }

    [MaxLength(32)]
    public string UnitLabel { get; set; } = string.Empty;

    [MaxLength(12)]
    public string NativeCurrency { get; set; } = "USD";

    public decimal PriceInUsd { get; set; }
    public decimal Price24hAgoInUsd { get; set; }
    public decimal High24hInUsd { get; set; }
    public decimal Low24hInUsd { get; set; }

    [MaxLength(4000)]
    public string SparklineJson { get; set; } = "[]";

    [MaxLength(80)]
    public string ProviderKey { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ProviderDisplayName { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Note { get; set; }

    public bool IsFallback { get; set; }
    public int SortOrder { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
