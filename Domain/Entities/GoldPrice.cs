using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class GoldPrice
{
    public int Id { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PricePerGram { get; set; }

    public DateTime EffectiveFrom { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
