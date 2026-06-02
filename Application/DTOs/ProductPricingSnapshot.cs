namespace MGold.Application.DTOs;

public class ProductPricingSnapshot
{
    public decimal GoldPricePerGram { get; set; }
    public decimal ProductWeight { get; set; }
    public decimal PurityRate { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal LaborCostPercentage { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public decimal CalculatedPurchasePrice { get; set; }
    public decimal CalculatedSalePrice { get; set; }
    public decimal AppliedUnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TotalCost { get; set; }
    public decimal ProfitOrLoss { get; set; }
}
