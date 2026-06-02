using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class CreateTransactionDto
{
    [Required]
    public int ProductId { get; set; }

    public int? CustomerId { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? UnitPrice { get; set; }

    public bool UseLiveCalculatedPrice { get; set; } = true;

    public DateTime? Date { get; set; }
}

public class UpdateTransactionDto : CreateTransactionDto
{
}

public class TransactionDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public TransactionType Type { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
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
