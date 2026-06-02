namespace MGold.Application.DTOs;

public class DashboardSummaryDto
{
    public string RangePreset { get; set; } = DashboardRangePresets.Monthly;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int TotalProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int TotalCustomers { get; set; }
    public int OpenOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int PendingReviews { get; set; }
    public int UnreadNotifications { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetProfitOrLoss { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DashboardAnalyticsDto Analytics { get; set; } = new();
    public IReadOnlyList<LowStockProductDto> LowStockItems { get; set; } = Array.Empty<LowStockProductDto>();
    public IReadOnlyList<OrderStatusCountDto> OrderStatusBreakdown { get; set; } = Array.Empty<OrderStatusCountDto>();
}

public static class DashboardRangePresets
{
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";
    public const string Yearly = "yearly";
    public const string Custom = "custom";

    public static readonly string[] All = [Daily, Weekly, Monthly, Yearly, Custom];
}

public class DashboardAnalyticsDto
{
    public DashboardFilterDto Filter { get; set; } = new();
    public DashboardKpiStripDto Kpis { get; set; } = new();
    public DashboardTrendPackDto Trends { get; set; } = new();
    public DashboardFinancialAnalyticsDto Financial { get; set; } = new();
    public DashboardProductAnalyticsDto Products { get; set; } = new();
    public DashboardTaskAnalyticsDto Workforce { get; set; } = new();
    public DashboardCustomerAnalyticsDto Customers { get; set; } = new();
}

public class DashboardFilterDto
{
    public string Preset { get; set; } = DashboardRangePresets.Monthly;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public IReadOnlyList<DashboardFilterOptionDto> AvailablePresets { get; set; } = [];
}

public class DashboardFilterOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class DashboardKpiStripDto
{
    public decimal Revenue { get; set; }
    public decimal Expense { get; set; }
    public decimal NetProfit { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int OrderCount { get; set; }
    public int BusyHour { get; set; }
    public string BusyHourLabel { get; set; } = string.Empty;
    public int CompletedTaskCount { get; set; }
}

public class DashboardTrendPackDto
{
    public DashboardTrendChartDto DailyOrders { get; set; } = new();
    public DashboardTrendChartDto WeeklyOrders { get; set; } = new();
    public DashboardTrendChartDto MonthlyOrders { get; set; } = new();
    public DashboardTrendChartDto YearlyOrders { get; set; } = new();
    public DashboardTrendChartDto CustomerHours { get; set; } = new();
    public DashboardTrendChartDto CustomerPeriods { get; set; } = new();
}

public class DashboardFinancialAnalyticsDto
{
    public DashboardTrendChartDto RevenueVsExpense { get; set; } = new();
    public DashboardTrendChartDto NetProfitTrend { get; set; } = new();
    public DashboardBreakdownChartDto RevenueComposition { get; set; } = new();
}

public class DashboardProductAnalyticsDto
{
    public DashboardBreakdownChartDto TopOrderedProducts { get; set; } = new();
    public DashboardBreakdownChartDto LowPerformingProducts { get; set; } = new();
    public DashboardBreakdownChartDto StockBurnRate { get; set; } = new();
    public DashboardBreakdownChartDto CategoryPerformance { get; set; } = new();
}

public class DashboardTaskAnalyticsDto
{
    public DashboardBreakdownChartDto TaskStatusDistribution { get; set; } = new();
    public DashboardBreakdownChartDto EmployeeTaskPerformance { get; set; } = new();
    public DashboardBreakdownChartDto EmployeeOrderWorkload { get; set; } = new();
}

public class DashboardCustomerAnalyticsDto
{
    public DashboardBreakdownChartDto PeakCustomerHours { get; set; } = new();
    public DashboardBreakdownChartDto PeakCustomerPeriods { get; set; } = new();
}

public class DashboardTrendChartDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ChartType { get; set; } = "line";
    public string ValueSuffix { get; set; } = string.Empty;
    public IReadOnlyList<DashboardSeriesDto> Series { get; set; } = [];
}

public class DashboardBreakdownChartDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ChartType { get; set; } = "bar";
    public string ValueSuffix { get; set; } = string.Empty;
    public IReadOnlyList<DashboardBreakdownItemDto> Items { get; set; } = [];
}

public class DashboardSeriesDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public IReadOnlyList<DashboardSeriesPointDto> Points { get; set; } = [];
}

public class DashboardSeriesPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Meta { get; set; }
}

public class DashboardBreakdownItemDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Detail { get; set; }
}

public class LowStockProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
}

public class OrderStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}
