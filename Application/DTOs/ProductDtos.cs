using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class CreateProductDto
{
    public int? CompanyId { get; set; }

    [Required]
    [MaxLength(120)]
    [RegularExpression(@".*\S.*", ErrorMessage = "Product name is required.")]
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
}

public class UpdateProductDto : CreateProductDto
{
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    public decimal Weight { get; set; }
    public decimal PurityRate { get; set; }
    public decimal LaborCost { get; set; }
    public decimal LaborCostPercentage { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsLowStock { get; set; }
    public ProductPricePreviewDto? CurrentPricing { get; set; }
}

public class ProductFilterDto
{
    public string? Search { get; set; }
    public ProductType? Type { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStockOnly { get; set; }
    public bool? LowStockOnly { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}

public class ProductPricePreviewDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public ProductType ProductType { get; set; }
    public decimal Weight { get; set; }
    public decimal PurityRate { get; set; }
    public decimal GoldPricePerGram { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal LaborCostPercentage { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public decimal RecommendedSalePrice { get; set; }
    public bool UsesLiveGoldPrice { get; set; }
}
