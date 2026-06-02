using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class CreateProductReviewDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    public int? CustomerId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Comment { get; set; } = string.Empty;
}

public class ModerateProductReviewDto
{
    [Required]
    public ReviewStatus Status { get; set; }

    [MaxLength(500)]
    public string? AdminReply { get; set; }
}

public class ProductReviewDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public ReviewStatus Status { get; set; }
    public string? AdminReply { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModeratedAt { get; set; }
}
