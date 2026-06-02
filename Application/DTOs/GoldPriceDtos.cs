using System.ComponentModel.DataAnnotations;

namespace MGold.Application.DTOs;

public class CreateGoldPriceDto
{
    [Range(0, double.MaxValue)]
    public decimal PricePerGram { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateGoldPriceDto : CreateGoldPriceDto
{
}

public class GoldPriceDto
{
    public int Id { get; set; }
    public decimal PricePerGram { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string? Source { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
