using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Domain.Entities;

public class Product
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ProductType Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Weight { get; set; }

    [Range(0, 1)]
    public decimal PurityRate { get; set; } = 1m;

    [Range(0, double.MaxValue)]
    public decimal LaborCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal LaborCostPercentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AdditionalCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ProfitMarginPercentage { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SalePrice { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public ICollection<CustomerFavorite> CustomerFavorites { get; set; } = new List<CustomerFavorite>();
}
