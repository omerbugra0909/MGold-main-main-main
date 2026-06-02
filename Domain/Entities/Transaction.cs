using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required]
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TotalPrice { get; set; }

    public decimal GoldPricePerGramSnapshot { get; set; }
    public decimal ProductWeightSnapshot { get; set; }
    public decimal PurityRateSnapshot { get; set; }
    public decimal MaterialCostSnapshot { get; set; }
    public decimal LaborCostSnapshot { get; set; }
    public decimal LaborCostPercentageSnapshot { get; set; }
    public decimal AdditionalCostSnapshot { get; set; }
    public decimal ProfitMarginPercentageSnapshot { get; set; }
    public decimal CalculatedPurchasePriceSnapshot { get; set; }
    public decimal CalculatedSalePriceSnapshot { get; set; }
    public decimal TotalCostSnapshot { get; set; }
    public decimal ProfitOrLoss { get; set; }

    public DateTime Date { get; set; }
}
