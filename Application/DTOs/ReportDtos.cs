namespace MGold.Application.DTOs;

public class ProfitLossSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCostOfSoldGoods { get; set; }
    public decimal TotalMaterialCostOfSales { get; set; }
    public decimal TotalLaborCostOfSales { get; set; }
    public decimal TotalAdditionalCostOfSales { get; set; }
    public decimal TotalInvestmentAmount { get; set; }
    public decimal InventoryEstimatedCost { get; set; }
    public decimal InventoryEstimatedRevenue { get; set; }
    public decimal NetProfitOrLoss { get; set; }
    public int TotalSalesTransactions { get; set; }
    public int TotalBuyTransactions { get; set; }
    public IReadOnlyList<ProfitLossByProductDto> ProductBreakdown { get; set; } = Array.Empty<ProfitLossByProductDto>();
}

public class ProfitLossByProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ProfitOrLoss { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
}
