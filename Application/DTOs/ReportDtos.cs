namespace MGold.Application.DTOs;

public class OperationalReportDto
{
    public string RangePreset { get; set; } = DashboardRangePresets.Monthly;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string ScopeTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public IReadOnlyList<ReportMetricDto> Metrics { get; set; } = [];
    public IReadOnlyList<ReportTableDto> Tables { get; set; } = [];
}

public class ReportMetricDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public class ReportTableDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Columns { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];
}

public class ReportArchiveItemDto
{
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
}

public class ReportPdfResultDto
{
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
}

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
