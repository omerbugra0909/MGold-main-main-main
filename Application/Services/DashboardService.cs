using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;
using TaskState = MGold.Domain.Enums.TaskStatus;

namespace MGold.Application.Services;

public class DashboardService(
    AppDbContext context,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService) : IDashboardService
{
    private sealed record OrderLineSnapshot(Order Order, OrderItem Item);

    private static readonly IReadOnlyList<DashboardFilterOptionDto> FilterOptions =
    [
        new() { Value = DashboardRangePresets.Daily, Label = "Günlük" },
        new() { Value = DashboardRangePresets.Weekly, Label = "Haftalık" },
        new() { Value = DashboardRangePresets.Monthly, Label = "Aylık" },
        new() { Value = DashboardRangePresets.Yearly, Label = "Yıllık" },
        new() { Value = DashboardRangePresets.Custom, Label = "Ozel Aralik" }
    ];

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        string? rangePreset = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var filter = NormalizeFilter(rangePreset, startDate, endDate);

        var ordersQuery = context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.AssignedEmployeeUser)
            .AsQueryable();
        var salesQuery = context.Transactions
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.Type == TransactionType.Sell)
            .AsQueryable();
        var productsQuery = context.Products.AsNoTracking().AsQueryable();
        var customersQuery = context.Customers.AsNoTracking().AsQueryable();
        var tasksQuery = context.WorkTasks
            .AsNoTracking()
            .Include(x => x.AssignedToUser)
            .AsQueryable();
        var notificationsQuery = context.Notifications.AsNoTracking().AsQueryable();
        var reviewsQuery = context.ProductReviews
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();

        if (currentUserService.CompanyId is int scopedCompanyId)
        {
            ordersQuery = ordersQuery.Where(x => x.CompanyId == scopedCompanyId);
            salesQuery = salesQuery.Where(x => x.CompanyId == scopedCompanyId);
            productsQuery = productsQuery.Where(x => x.CompanyId == scopedCompanyId);
            customersQuery = customersQuery.Where(x => x.CompanyId == scopedCompanyId);
            tasksQuery = tasksQuery.Where(x => x.CompanyId == scopedCompanyId);
            reviewsQuery = reviewsQuery.Where(x => x.Product.CompanyId == scopedCompanyId);
            notificationsQuery = notificationsQuery.Where(x =>
                x.TargetRole == null ||
                x.TargetRole == currentUserService.Role ||
                x.TargetRole == RoleConstants.ManagerOnly);
            notificationsQuery = await ApplyNotificationCompanyScopeAsync(notificationsQuery, scopedCompanyId, cancellationToken);
        }
        else if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            notificationsQuery = notificationsQuery.Where(x => x.TargetRole == currentUserService.Role);
        }

        var allOrders = await ordersQuery.ToListAsync(cancellationToken);
        var allSales = await salesQuery.ToListAsync(cancellationToken);
        var allProducts = await productsQuery.ToListAsync(cancellationToken);
        var allCustomers = await customersQuery.ToListAsync(cancellationToken);
        var allTasks = await tasksQuery.ToListAsync(cancellationToken);
        var unreadNotifications = await notificationsQuery.CountAsync(x => !x.IsRead, cancellationToken);
        var pendingReviews = await reviewsQuery.CountAsync(x => x.Status == ReviewStatus.Pending, cancellationToken);

        var filteredOrders = allOrders
            .Where(x => x.CreatedAt >= filter.StartDate && x.CreatedAt <= filter.EndDate)
            .ToList();
        var filteredSales = allSales
            .Where(x => x.Date >= filter.StartDate && x.Date <= filter.EndDate)
            .ToList();
        var filteredTasks = allTasks
            .Where(x => x.CreatedAt >= filter.StartDate && x.CreatedAt <= filter.EndDate)
            .ToList();
        var filteredOrderItems = filteredOrders
            .SelectMany(x => x.Items.Select(item => new OrderLineSnapshot(x, item)))
            .ToList();

        var totalRevenue = filteredSales.Sum(x => x.TotalPrice);
        var totalExpense = filteredSales.Sum(x => x.TotalCostSnapshot);
        var totalProfit = filteredSales.Sum(x => x.ProfitOrLoss);
        var averageOrderValue = filteredOrders.Count == 0 ? 0 : filteredOrders.Average(x => x.TotalAmount);
        var customerHourPeak = filteredOrders
            .GroupBy(x => x.CreatedAt.Hour)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();

        return new DashboardSummaryDto
        {
            RangePreset = filter.Preset,
            StartDate = filter.StartDate,
            EndDate = filter.EndDate,
            GeneratedAt = DateTime.UtcNow,
            TotalProducts = allProducts.Count,
            LowStockProducts = allProducts.Count(x => x.StockQuantity <= 5),
            TotalCustomers = allCustomers.Count,
            OpenOrders = filteredOrders.Count(x => x.Status is OrderStatus.Preparing or OrderStatus.Ready),
            CompletedOrders = filteredOrders.Count(x => x.Status == OrderStatus.Completed),
            PendingReviews = pendingReviews,
            UnreadNotifications = unreadNotifications,
            TotalTasks = filteredTasks.Count,
            CompletedTasks = filteredTasks.Count(x => x.Status == TaskState.Completed),
            PendingTasks = filteredTasks.Count(x => x.Status != TaskState.Completed),
            TotalRevenue = totalRevenue,
            TotalExpense = totalExpense,
            NetProfitOrLoss = totalProfit,
            AverageOrderValue = averageOrderValue,
            LowStockItems = allProducts.Where(x => x.StockQuantity <= 5)
                .OrderBy(x => x.StockQuantity)
                .Take(6)
                .Select(x => new LowStockProductDto
                {
                    ProductId = x.Id,
                    ProductName = x.Name,
                    StockQuantity = x.StockQuantity
                })
                .ToList(),
            OrderStatusBreakdown = filteredOrders.GroupBy(x => x.Status)
                .Select(group => new OrderStatusCountDto
                {
                    Status = group.Key.ToString(),
                    Count = group.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList(),
            Analytics = new DashboardAnalyticsDto
            {
                Filter = new DashboardFilterDto
                {
                    Preset = filter.Preset,
                    StartDate = filter.StartDate,
                    EndDate = filter.EndDate,
                    AvailablePresets = FilterOptions
                },
                Kpis = new DashboardKpiStripDto
                {
                    Revenue = totalRevenue,
                    Expense = totalExpense,
                    NetProfit = totalProfit,
                    AverageOrderValue = averageOrderValue,
                    OrderCount = filteredOrders.Count,
                    BusyHour = customerHourPeak?.Key ?? 0,
                    BusyHourLabel = customerHourPeak is null ? "Veri yok" : $"{customerHourPeak.Key:00}:00 - {customerHourPeak.Key:00}:59",
                    CompletedTaskCount = filteredTasks.Count(x => x.Status == TaskState.Completed)
                },
                Trends = BuildTrendPack(filter, filteredOrders),
                Financial = BuildFinancialAnalytics(filter, filteredSales),
                Products = BuildProductAnalytics(allProducts, filteredOrderItems, filteredSales),
                Workforce = BuildTaskAnalytics(filteredTasks, filteredOrders),
                Customers = BuildCustomerAnalytics(filteredOrders)
            }
        };
    }

    private static DashboardFilterDto NormalizeFilter(string? preset, DateTime? startDate, DateTime? endDate)
    {
        var normalizedPreset = string.IsNullOrWhiteSpace(preset)
            ? DashboardRangePresets.Monthly
            : preset.Trim().ToLowerInvariant();
        if (!DashboardRangePresets.All.Contains(normalizedPreset))
        {
            normalizedPreset = DashboardRangePresets.Monthly;
        }

        var utcToday = DateTime.UtcNow.Date;
        DateTime start;
        DateTime end;

        switch (normalizedPreset)
        {
            case DashboardRangePresets.Daily:
                start = utcToday.AddDays(-6);
                end = utcToday.AddDays(1).AddTicks(-1);
                break;
            case DashboardRangePresets.Weekly:
                start = utcToday.AddDays(-41);
                end = utcToday.AddDays(1).AddTicks(-1);
                break;
            case DashboardRangePresets.Yearly:
                start = new DateTime(utcToday.Year - 1, utcToday.Month, 1);
                end = utcToday.AddDays(1).AddTicks(-1);
                break;
            case DashboardRangePresets.Custom:
                start = (startDate ?? utcToday.AddDays(-29)).Date;
                end = (endDate ?? utcToday).Date.AddDays(1).AddTicks(-1);
                break;
            default:
                start = utcToday.AddDays(-29);
                end = utcToday.AddDays(1).AddTicks(-1);
                break;
        }

        if (start > end)
        {
            (start, end) = (end.Date, start.Date.AddDays(1).AddTicks(-1));
        }

        return new DashboardFilterDto
        {
            Preset = normalizedPreset,
            StartDate = start,
            EndDate = end,
            AvailablePresets = FilterOptions
        };
    }

    private static DashboardTrendPackDto BuildTrendPack(DashboardFilterDto filter, IReadOnlyList<Order> orders)
        => new()
        {
            DailyOrders = new DashboardTrendChartDto
            {
                Title = "Günlük Sipariş Trendi",
                Subtitle = "Son 7 gün içindeki sipariş dalgalanmasi",
                ChartType = "line",
                ValueSuffix = " sipariş",
                Series =
                [
                    new DashboardSeriesDto
                    {
                        Key = "daily-orders",
                        Label = "Sipariş",
                        Color = "#e3c991",
                        Points = BuildTimeSeries(
                            filter.EndDate.Date.AddDays(-6),
                            7,
                            date => date.ToString("dd.MM"),
                            orders)
                    }
                ]
            },
            WeeklyOrders = new DashboardTrendChartDto
            {
                Title = "Haftalık Sipariş Trendi",
                Subtitle = "Son 8 haftanın yoğunluk karşılaştırması",
                ChartType = "line",
                ValueSuffix = " sipariş",
                Series =
                [
                    new DashboardSeriesDto
                    {
                        Key = "weekly-orders",
                        Label = "Sipariş",
                        Color = "#8fc5da",
                        Points = BuildWeeklyOrderPoints(orders, 8)
                    }
                ]
            },
            MonthlyOrders = new DashboardTrendChartDto
            {
                Title = "Aylık Sipariş Trendi",
                Subtitle = "Ay bazli sipariş hacmi",
                ChartType = "line",
                ValueSuffix = " sipariş",
                Series =
                [
                    new DashboardSeriesDto
                    {
                        Key = "monthly-orders",
                        Label = "Sipariş",
                        Color = "#c79b53",
                        Points = BuildMonthlyOrderPoints(orders, 6)
                    }
                ]
            },
            YearlyOrders = new DashboardTrendChartDto
            {
                Title = "Yıllık Sipariş Trendi",
                Subtitle = "Yil bazli hacim görünumu",
                ChartType = "line",
                ValueSuffix = " sipariş",
                Series =
                [
                    new DashboardSeriesDto
                    {
                        Key = "yearly-orders",
                        Label = "Sipariş",
                        Color = "#81d4a1",
                        Points = orders.GroupBy(x => x.CreatedAt.Year)
                            .OrderBy(group => group.Key)
                            .Select(group => new DashboardSeriesPointDto
                            {
                                Label = group.Key.ToString(),
                                Value = group.Count()
                            })
                            .ToList()
                    }
                ]
            },
            CustomerHours = new DashboardTrendChartDto
            {
                Title = "Müşteri Sipariş Saatleri",
                Subtitle = "Gun içindeki yoğun sipariş anlari",
                ChartType = "scatter",
                ValueSuffix = " sipariş",
                Series =
                [
                    new DashboardSeriesDto
                    {
                        Key = "customer-hours",
                        Label = "Saatlik yoğunluk",
                        Color = "#ffb17d",
                        Points = Enumerable.Range(0, 24)
                            .Select(hour => new DashboardSeriesPointDto
                            {
                                Label = $"{hour:00}:00",
                                Value = orders.Count(x => x.CreatedAt.Hour == hour)
                            })
                            .ToList()
                    }
                ]
            },
            CustomerPeriods = new DashboardTrendChartDto
            {
                Title = "Dönemsel Kullanim Trendi",
                Subtitle = "Haftanin günlerine göre sipariş kullanim yoğunluğu",
                ChartType = "bar",
                ValueSuffix = " sipariş",
                Series =
                [
                    new DashboardSeriesDto
                    {
                        Key = "customer-periods",
                        Label = "Gun bazli yoğunluk",
                        Color = "#b8a7ff",
                        Points = Enum.GetValues<DayOfWeek>()
                            .Select(day => new DashboardSeriesPointDto
                            {
                                Label = DayLabel(day),
                                Value = orders.Count(x => x.CreatedAt.DayOfWeek == day)
                            })
                            .ToList()
                    }
                ]
            }
        };

    private static DashboardFinancialAnalyticsDto BuildFinancialAnalytics(DashboardFilterDto filter, IReadOnlyList<Transaction> sales)
    {
        var revenueSeries = BuildDailyFinancialSeries(filter.StartDate, filter.EndDate, sales, x => x.TotalPrice);
        var expenseSeries = BuildDailyFinancialSeries(filter.StartDate, filter.EndDate, sales, x => x.TotalCostSnapshot);
        var profitSeries = BuildDailyFinancialSeries(filter.StartDate, filter.EndDate, sales, x => x.ProfitOrLoss);

        return new DashboardFinancialAnalyticsDto
        {
            RevenueVsExpense = new DashboardTrendChartDto
            {
                Title = "Gelir / Gider Dengesi",
                Subtitle = "Gun bazli finansal akış",
                ChartType = "line",
                ValueSuffix = " TL",
                Series =
                [
                    new DashboardSeriesDto { Key = "revenue", Label = "Gelir", Color = "#e3c991", Points = revenueSeries },
                    new DashboardSeriesDto { Key = "expense", Label = "Gider", Color = "#ff8d8d", Points = expenseSeries }
                ]
            },
            NetProfitTrend = new DashboardTrendChartDto
            {
                Title = "Net Kâr Trendi",
                Subtitle = "Kar/zarar momentümunu izler",
                ChartType = "line",
                ValueSuffix = " TL",
                Series =
                [
                    new DashboardSeriesDto { Key = "profit", Label = "Net Kâr", Color = "#81d4a1", Points = profitSeries }
                ]
            },
            RevenueComposition = new DashboardBreakdownChartDto
            {
                Title = "Finansal Kompozisyon",
                Subtitle = "Dönem özetinde gelir, gider ve net kâr",
                ChartType = "bar",
                ValueSuffix = " TL",
                Items =
                [
                    new DashboardBreakdownItemDto { Label = "Toplam Gelir", Value = sales.Sum(x => x.TotalPrice), Detail = "Siparişlerden gelen brut ciro" },
                    new DashboardBreakdownItemDto { Label = "Toplam Gider", Value = sales.Sum(x => x.TotalCostSnapshot), Detail = "Maliyet ve operasyonel gider etkisi" },
                    new DashboardBreakdownItemDto { Label = "Net Kâr", Value = sales.Sum(x => x.ProfitOrLoss), Detail = "Toplam kâr/zarar dengesi" }
                ]
            }
        };
    }

    private static DashboardProductAnalyticsDto BuildProductAnalytics(
        IReadOnlyList<Product> products,
        IReadOnlyList<OrderLineSnapshot> filteredOrderItems,
        IReadOnlyList<Transaction> sales)
    {
        var topOrdered = filteredOrderItems
            .GroupBy(x => x.Item.Product?.Name ?? x.Item.ProductId.ToString())
            .Select(group => new DashboardBreakdownItemDto
            {
                Label = group.Key,
                Value = group.Sum(x => (decimal)x.Item.Quantity),
                Detail = $"{group.Sum(x => (decimal)x.Item.TotalPrice):N0} TL sipariş hacmi"
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        var revenueByProductId = sales
            .GroupBy(x => x.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.TotalPrice));
        var soldQuantityByProductId = sales
            .GroupBy(x => x.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Quantity));

        var lowPerforming = products
            .Select(product => new DashboardBreakdownItemDto
            {
                Label = product.Name,
                Value = revenueByProductId.TryGetValue(product.Id, out var revenue) ? revenue : 0,
                Detail = $"Stok {product.StockQuantity} • Satin alma hizi düşük"
            })
            .OrderBy(x => x.Value)
            .ThenByDescending(x => products.First(p => p.Name == x.Label).StockQuantity)
            .Take(8)
            .ToList();

        var burnRate = products
            .Select(product =>
            {
                var sold = soldQuantityByProductId.TryGetValue(product.Id, out var quantity) ? quantity : 0;
                var denominator = Math.Max(product.StockQuantity + sold, 1);
                return new DashboardBreakdownItemDto
                {
                    Label = product.Name,
                    Value = Math.Round((decimal)sold / denominator * 100m, 2),
                    Detail = $"{sold} adet satildi • Mevcut stok {product.StockQuantity}"
                };
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        var categoryPerformance = products
            .GroupJoin(
                sales.GroupBy(x => x.Product?.Type ?? ProductType.Gold).ToDictionary(group => group.Key, group => group.Sum(x => x.TotalPrice)),
                product => product.Type,
                revenue => revenue.Key,
                (product, revenue) => new { product.Type, Revenue = revenue.Select(x => x.Value).FirstOrDefault() })
            .GroupBy(x => x.Type)
            .Select(group => new DashboardBreakdownItemDto
            {
                Label = group.Key.ToString(),
                Value = group.Max(x => x.Revenue),
                Detail = $"{group.Count()} ürün kategoride bulunuyor"
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        return new DashboardProductAnalyticsDto
        {
            TopOrderedProducts = new DashboardBreakdownChartDto
            {
                Title = "En Çok Sipariş Edilen Ürünler",
                Subtitle = "Sipariş adedi bazli liderler",
                ChartType = "bar",
                ValueSuffix = " adet",
                Items = topOrdered
            },
            LowPerformingProducts = new DashboardBreakdownChartDto
            {
                Title = "Düşük Performansli Ürünler",
                Subtitle = "Stokta duran ve gelir katkisi zayif ürünler",
                ChartType = "bar",
                ValueSuffix = " TL",
                Items = lowPerforming
            },
            StockBurnRate = new DashboardBreakdownChartDto
            {
                Title = "Stok Tüketim Hizi",
                Subtitle = "Satilan miktarin toplam stok havuzuna etkisi",
                ChartType = "dot",
                ValueSuffix = "%",
                Items = burnRate
            },
            CategoryPerformance = new DashboardBreakdownChartDto
            {
                Title = "Kategori Performansi",
                Subtitle = "Kategori bazli ciro katkisi",
                ChartType = "bar",
                ValueSuffix = " TL",
                Items = categoryPerformance
            }
        };
    }

    private static DashboardTaskAnalyticsDto BuildTaskAnalytics(IReadOnlyList<WorkTask> tasks, IReadOnlyList<Order> orders)
    {
        var taskStatus = Enum.GetValues<TaskState>()
            .Select(status => new DashboardBreakdownItemDto
            {
                Label = status switch
                {
                    TaskState.Waiting => "Bekliyor",
                    TaskState.InProgress => "Yapiliyor",
                    _ => "Tamamlandi"
                },
                Value = tasks.Count(x => x.Status == status),
                Detail = "Görev dagilim durumu"
            })
            .ToList();

        var employeeTaskPerformance = tasks
            .GroupBy(x => x.AssignedToUser?.FullName ?? "Atanmamis")
            .Select(group => new DashboardBreakdownItemDto
            {
                Label = group.Key,
                Value = group.Count(x => x.Status == TaskState.Completed),
                Detail = $"{group.Count()} toplam görev"
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        var employeeOrderWorkload = orders
            .Where(x => x.AssignedEmployeeUser is not null)
            .GroupBy(x => x.AssignedEmployeeUser!.FullName)
            .Select(group => new DashboardBreakdownItemDto
            {
                Label = group.Key,
                Value = group.Count(),
                Detail = $"{group.Count(x => x.Status == OrderStatus.Completed)} tamamlanan sipariş"
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        return new DashboardTaskAnalyticsDto
        {
            TaskStatusDistribution = new DashboardBreakdownChartDto
            {
                Title = "Görev Durum Dagilimi",
                Subtitle = "Bekleyen, devam eden ve tamamlanan görevler",
                ChartType = "bar",
                ValueSuffix = " görev",
                Items = taskStatus
            },
            EmployeeTaskPerformance = new DashboardBreakdownChartDto
            {
                Title = "Çalışan Görev Performansi",
                Subtitle = "Tamamlanan görev odakli performans",
                ChartType = "bar",
                ValueSuffix = " görev",
                Items = employeeTaskPerformance
            },
            EmployeeOrderWorkload = new DashboardBreakdownChartDto
            {
                Title = "İşlem Yoğunlugu",
                Subtitle = "Çalışan bazli sipariş akış yoğunluğu",
                ChartType = "bar",
                ValueSuffix = " sipariş",
                Items = employeeOrderWorkload
            }
        };
    }

    private static DashboardCustomerAnalyticsDto BuildCustomerAnalytics(IReadOnlyList<Order> orders)
    {
        var hourItems = Enumerable.Range(0, 24)
            .Select(hour => new DashboardBreakdownItemDto
            {
                Label = $"{hour:00}:00",
                Value = orders.Count(x => x.CreatedAt.Hour == hour),
                Detail = "Bu saat dilimindeki sipariş adedi"
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        var periodItems = Enum.GetValues<DayOfWeek>()
            .Select(day => new DashboardBreakdownItemDto
            {
                Label = DayLabel(day),
                Value = orders.Count(x => x.CreatedAt.DayOfWeek == day),
                Detail = "Haftalık kullanim yoğunluğu"
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        return new DashboardCustomerAnalyticsDto
        {
            PeakCustomerHours = new DashboardBreakdownChartDto
            {
                Title = "Müşteri Sipariş Yoğunluk Saatleri",
                Subtitle = "Gun içinde en çok sipariş açılan saatler",
                ChartType = "dot",
                ValueSuffix = " sipariş",
                Items = hourItems
            },
            PeakCustomerPeriods = new DashboardBreakdownChartDto
            {
                Title = "Dönemsel Kullanim Yoğunlugu",
                Subtitle = "Haftanin günlerine göre kullanim dagilimi",
                ChartType = "bar",
                ValueSuffix = " sipariş",
                Items = periodItems
            }
        };
    }

    private static IReadOnlyList<DashboardSeriesPointDto> BuildTimeSeries(
        DateTime start,
        int count,
        Func<DateTime, string> labelFactory,
        IReadOnlyList<Order> orders)
        => Enumerable.Range(0, count)
            .Select(offset => start.AddDays(offset))
            .Select(date => new DashboardSeriesPointDto
            {
                Label = labelFactory(date),
                Value = orders.Count(order => order.CreatedAt.Date == date.Date)
            })
            .ToList();

    private static IReadOnlyList<DashboardSeriesPointDto> BuildWeeklyOrderPoints(IReadOnlyList<Order> orders, int weekCount)
    {
        var currentWeekStart = StartOfWeek(DateTime.UtcNow.Date);
        return Enumerable.Range(0, weekCount)
            .Select(offset => currentWeekStart.AddDays((offset - (weekCount - 1)) * 7))
            .Select(weekStart => new DashboardSeriesPointDto
            {
                Label = $"{weekStart:dd.MM}",
                Value = orders.Count(order => order.CreatedAt.Date >= weekStart && order.CreatedAt.Date < weekStart.AddDays(7))
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardSeriesPointDto> BuildMonthlyOrderPoints(IReadOnlyList<Order> orders, int monthCount)
    {
        var currentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        return Enumerable.Range(0, monthCount)
            .Select(offset => currentMonthStart.AddMonths(offset - (monthCount - 1)))
            .Select(monthStart => new DashboardSeriesPointDto
            {
                Label = monthStart.ToString("MMM yy"),
                Value = orders.Count(order => order.CreatedAt.Year == monthStart.Year && order.CreatedAt.Month == monthStart.Month)
            })
            .ToList();
    }

    private static IReadOnlyList<DashboardSeriesPointDto> BuildDailyFinancialSeries(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<Transaction> sales,
        Func<Transaction, decimal> selector)
    {
        var days = (int)Math.Min((endDate.Date - startDate.Date).TotalDays + 1, 31);
        var seriesStart = endDate.Date.AddDays(-(days - 1));
        return Enumerable.Range(0, days)
            .Select(offset => seriesStart.AddDays(offset))
            .Select(date => new DashboardSeriesPointDto
            {
                Label = date.ToString("dd.MM"),
                Value = sales.Where(x => x.Date.Date == date.Date).Sum(selector)
            })
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var delta = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-delta);
    }

    private static string DayLabel(DayOfWeek day)
        => day switch
        {
            DayOfWeek.Monday => "Pzt",
            DayOfWeek.Tuesday => "Sali",
            DayOfWeek.Wednesday => "Cars",
            DayOfWeek.Thursday => "Pers",
            DayOfWeek.Friday => "Cuma",
            DayOfWeek.Saturday => "Cts",
            _ => "Paz"
        };

    private async Task<IQueryable<Notification>> ApplyNotificationCompanyScopeAsync(
        IQueryable<Notification> query,
        int companyId,
        CancellationToken cancellationToken)
    {
        var orderIds = await context.Orders
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);
        var productIds = await context.Products
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);
        var reviewIds = await context.ProductReviews
            .AsNoTracking()
            .Where(x => x.Product.CompanyId == companyId)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);

        return query.Where(x =>
            x.RelatedEntityName == null
            || (x.RelatedEntityName == nameof(Order) && x.RelatedEntityId != null && orderIds.Contains(x.RelatedEntityId))
            || (x.RelatedEntityName == nameof(Product) && x.RelatedEntityId != null && productIds.Contains(x.RelatedEntityId))
            || (x.RelatedEntityName == nameof(ProductReview) && x.RelatedEntityId != null && reviewIds.Contains(x.RelatedEntityId)));
    }
}
