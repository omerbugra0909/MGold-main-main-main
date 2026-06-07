using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;
using TaskState = MGold.Domain.Enums.TaskStatus;

namespace MGold.Application.Services;

public class ReportService(
    AppDbContext context,
    IWebHostEnvironment environment,
    IOptions<CompanyProfileSettings> companyOptions,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService) : IReportService
{
    private static readonly IReadOnlyList<DashboardFilterOptionDto> FilterOptions =
    [
        new() { Value = DashboardRangePresets.Daily, Label = "Günlük" },
        new() { Value = DashboardRangePresets.Weekly, Label = "Haftalık" },
        new() { Value = DashboardRangePresets.Monthly, Label = "Aylık" },
        new() { Value = DashboardRangePresets.Yearly, Label = "Yıllık" }
    ];

    private readonly CompanyProfileSettings _companyProfile = companyOptions.Value;

    public async Task<OperationalReportDto> GetOperationalReportAsync(
        string? rangePreset,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var filter = NormalizeFilter(rangePreset, startDate, endDate);
        var companyId = currentUserService.IsInRole(RoleConstants.SystemAdmin) ? null : currentUserService.CompanyId;
        var company = companyId.HasValue
            ? await context.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId.Value, cancellationToken)
            : null;
        var scopeTitle = companyId.HasValue ? "Firma Raporu" : "Platform Genel Raporu";
        var companyName = company?.Name ?? _companyProfile.Name;

        var productsQuery = context.Products.AsNoTracking().Include(x => x.Company).AsQueryable();
        var ordersQuery = context.Orders.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.AssignedEmployeeUser)
            .AsQueryable();
        var customersQuery = context.Customers.AsNoTracking().AsQueryable();
        var tasksQuery = context.WorkTasks.AsNoTracking().Include(x => x.AssignedToUser).AsQueryable();
        var notificationsQuery = context.Notifications.AsNoTracking().AsQueryable();
        var reviewsQuery = context.ProductReviews.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .AsQueryable();
        var salesQuery = context.Transactions.AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.Type == TransactionType.Sell)
            .AsQueryable();

        if (companyId.HasValue)
        {
            productsQuery = productsQuery.Where(x => x.CompanyId == companyId.Value);
            ordersQuery = ordersQuery.Where(x => x.CompanyId == companyId.Value);
            customersQuery = customersQuery.Where(x => x.CompanyId == companyId.Value);
            tasksQuery = tasksQuery.Where(x => x.CompanyId == companyId.Value);
            reviewsQuery = reviewsQuery.Where(x => x.Product.CompanyId == companyId.Value);
            salesQuery = salesQuery.Where(x => x.CompanyId == companyId.Value);
            notificationsQuery = notificationsQuery.Where(x =>
                x.TargetRole == null ||
                x.TargetRole == currentUserService.Role ||
                x.TargetRole == RoleConstants.ManagerOnly);
        }

        var products = await productsQuery.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var orders = await ordersQuery
            .Where(x => x.CreatedAt >= filter.StartDate && x.CreatedAt <= filter.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var customers = await customersQuery.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var tasks = await tasksQuery
            .Where(x => x.CreatedAt >= filter.StartDate && x.CreatedAt <= filter.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var notifications = await notificationsQuery
            .Where(x => x.CreatedAt >= filter.StartDate && x.CreatedAt <= filter.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var reviews = await reviewsQuery
            .Where(x => x.CreatedAt >= filter.StartDate && x.CreatedAt <= filter.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var sales = await salesQuery
            .Where(x => x.Date >= filter.StartDate && x.Date <= filter.EndDate)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

        var totalRevenue = sales.Sum(x => x.TotalPrice);
        var totalCost = sales.Sum(x => x.TotalCostSnapshot);
        var netProfit = sales.Sum(x => x.ProfitOrLoss);
        var lowStockProducts = products.Where(x => x.StockQuantity <= 5).OrderBy(x => x.StockQuantity).ToList();
        var unreadNotifications = notifications.Count(x => !x.IsRead);
        var pendingReviews = reviews.Count(x => x.Status == ReviewStatus.Pending);

        var metrics = new List<ReportMetricDto>
        {
            Metric("Ürün", products.Count.ToString("N0", CultureInfo.CurrentCulture), $"{lowStockProducts.Count} düşük stok"),
            Metric("Sipariş", orders.Count.ToString("N0", CultureInfo.CurrentCulture), $"{orders.Count(x => x.Status == OrderStatus.Completed)} tamamlandı"),
            Metric("Satış", Money(totalRevenue), $"{sales.Count} işlem"),
            Metric("Net kâr", Money(netProfit), $"Maliyet {Money(totalCost)}"),
            Metric("Müşteri", customers.Count.ToString("N0", CultureInfo.CurrentCulture), "Kayıtlı müşteri"),
            Metric("Görev", tasks.Count.ToString("N0", CultureInfo.CurrentCulture), $"{tasks.Count(x => x.Status == TaskState.Completed)} tamamlandı"),
            Metric("Bildirim", notifications.Count.ToString("N0", CultureInfo.CurrentCulture), $"{unreadNotifications} okunmamış"),
            Metric("Yorum", reviews.Count.ToString("N0", CultureInfo.CurrentCulture), $"{pendingReviews} moderasyon bekliyor")
        };

        return new OperationalReportDto
        {
            RangePreset = filter.Preset,
            StartDate = filter.StartDate,
            EndDate = filter.EndDate,
            GeneratedAt = DateTime.UtcNow,
            ScopeTitle = scopeTitle,
            CompanyName = companyName,
            Metrics = metrics,
            Tables =
            [
                BuildSystemStatsTable(products, orders, customers, tasks, notifications, reviews, sales, lowStockProducts),
                BuildProductsTable(products),
                BuildStockTable(lowStockProducts.Count == 0 ? products.OrderBy(x => x.StockQuantity).Take(15).ToList() : lowStockProducts),
                BuildOrdersTable(orders),
                BuildSalesTable(sales),
                BuildCustomersTable(customers, orders),
                BuildTasksTable(tasks),
                BuildNotificationsTable(notifications),
                BuildReviewsTable(reviews)
            ]
        };
    }

    public async Task<ReportPdfResultDto> GenerateOperationalReportPdfAsync(
        string? rangePreset,
        DateTime? startDate,
        DateTime? endDate,
        bool archive,
        CancellationToken cancellationToken = default)
    {
        var report = await GetOperationalReportAsync(rangePreset, startDate, endDate, cancellationToken);
        var fileName = BuildReportFileName(report);
        var content = BuildReportPdf(report);

        if (archive)
        {
            Directory.CreateDirectory(GetArchiveDirectory());
            await File.WriteAllBytesAsync(Path.Combine(GetArchiveDirectory(), fileName), content, cancellationToken);
        }

        return new ReportPdfResultDto { Content = content, FileName = fileName };
    }

    public Task<IReadOnlyList<ReportArchiveItemDto>> GetArchivedReportsAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var directory = GetArchiveDirectory();
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<ReportArchiveItemDto>>([]);
        }

        IReadOnlyList<ReportArchiveItemDto> items = Directory
            .EnumerateFiles(directory, "*.pdf")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(file => new ReportArchiveItemDto
            {
                FileName = file.Name,
                Title = Path.GetFileNameWithoutExtension(file.Name).Replace('-', ' '),
                CreatedAt = file.CreationTimeUtc,
                SizeBytes = file.Length
            })
            .ToList();

        return Task.FromResult(items);
    }

    public async Task<ReportPdfResultDto> GetArchivedReportAsync(string fileName, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var safeFileName = Path.GetFileName(fileName);
        if (!safeFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException("Rapor dosyası bulunamadı.");
        }

        var path = Path.Combine(GetArchiveDirectory(), safeFileName);
        if (!File.Exists(path))
        {
            throw new KeyNotFoundException("Rapor dosyası bulunamadı.");
        }

        return new ReportPdfResultDto
        {
            Content = await File.ReadAllBytesAsync(path, cancellationToken),
            FileName = safeFileName
        };
    }

    public async Task<ProfitLossSummaryDto> GetProfitLossSummaryAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var query = context.Transactions
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();
        var productsQuery = context.Products.AsNoTracking().AsQueryable();

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
            productsQuery = productsQuery.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.Date >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.Date <= endDate.Value.ToUniversalTime());
        }

        var transactions = await query.ToListAsync(cancellationToken);

        var sales = transactions.Where(x => x.Type == TransactionType.Sell).ToList();
        var buys = transactions.Where(x => x.Type == TransactionType.Buy).ToList();

        var products = await productsQuery.ToListAsync(cancellationToken);
        var latestGoldPrice = await context.GoldPrices
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        decimal CalculateCurrentPurchaseCost(Product product)
        {
            var materialCost = product.Type == ProductType.Gold
                ? product.Weight * product.PurityRate * (latestGoldPrice?.PricePerGram ?? 0m)
                : product.PurchasePrice;

            var laborPercentageCost = materialCost * product.LaborCostPercentage / 100m;
            return materialCost + product.LaborCost + laborPercentageCost + product.AdditionalCost;
        }

        decimal CalculateCurrentSalePrice(Product product, decimal purchaseCost)
            => product.SalePrice > 0 ? product.SalePrice : purchaseCost * (1 + product.ProfitMarginPercentage / 100m);

        return new ProfitLossSummaryDto
        {
            TotalRevenue = sales.Sum(x => x.TotalPrice),
            TotalCostOfSoldGoods = sales.Sum(x => x.TotalCostSnapshot),
            TotalMaterialCostOfSales = sales.Sum(x => x.MaterialCostSnapshot * x.Quantity),
            TotalLaborCostOfSales = sales.Sum(x => x.LaborCostSnapshot * x.Quantity),
            TotalAdditionalCostOfSales = sales.Sum(x => x.AdditionalCostSnapshot * x.Quantity),
            TotalInvestmentAmount = buys.Sum(x => x.TotalPrice),
            InventoryEstimatedCost = products.Sum(x => x.StockQuantity * CalculateCurrentPurchaseCost(x)),
            InventoryEstimatedRevenue = products.Sum(x => x.StockQuantity * CalculateCurrentSalePrice(x, CalculateCurrentPurchaseCost(x))),
            NetProfitOrLoss = sales.Sum(x => x.ProfitOrLoss),
            TotalSalesTransactions = sales.Count,
            TotalBuyTransactions = buys.Count,
            ProductBreakdown = sales
                .GroupBy(x => new { x.ProductId, ProductName = x.Product!.Name })
                .Select(group => new ProfitLossByProductDto
                {
                    ProductId = group.Key.ProductId,
                    ProductName = group.Key.ProductName,
                    ProfitOrLoss = group.Sum(x => x.ProfitOrLoss),
                    Revenue = group.Sum(x => x.TotalPrice),
                    Cost = group.Sum(x => x.TotalCostSnapshot)
                })
                .OrderByDescending(x => x.ProfitOrLoss)
                .ToList()
        };
    }

    private static DashboardFilterDto NormalizeFilter(string? preset, DateTime? startDate, DateTime? endDate)
    {
        var normalizedPreset = string.IsNullOrWhiteSpace(preset)
            ? DashboardRangePresets.Monthly
            : preset.Trim().ToLowerInvariant();

        if (!FilterOptions.Any(x => x.Value == normalizedPreset))
        {
            normalizedPreset = DashboardRangePresets.Monthly;
        }

        var today = DateTime.UtcNow.Date;
        var start = normalizedPreset switch
        {
            DashboardRangePresets.Daily => today,
            DashboardRangePresets.Weekly => today.AddDays(-6),
            DashboardRangePresets.Yearly => new DateTime(today.Year, 1, 1),
            _ => new DateTime(today.Year, today.Month, 1)
        };
        var end = today.AddDays(1).AddTicks(-1);

        if (startDate.HasValue)
        {
            start = startDate.Value.Date.ToUniversalTime();
        }

        if (endDate.HasValue)
        {
            end = endDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
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

    private static ReportMetricDto Metric(string label, string value, string detail)
        => new() { Label = label, Value = value, Detail = detail };

    private static ReportTableDto Table(string key, string title, string description, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
        => new() { Key = key, Title = title, Description = description, Columns = columns, Rows = rows };

    private static ReportTableDto BuildSystemStatsTable(
        IReadOnlyList<Product> products,
        IReadOnlyList<Order> orders,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<WorkTask> tasks,
        IReadOnlyList<Notification> notifications,
        IReadOnlyList<ProductReview> reviews,
        IReadOnlyList<Transaction> sales,
        IReadOnlyList<Product> lowStockProducts)
        => Table(
            "system",
            "Genel Sistem İstatistikleri",
            "Seçili dönem ve erişim kapsamına göre operasyon özeti.",
            ["Gösterge", "Değer", "Detay"],
            [
                ["Toplam ürün", products.Count.ToString("N0", CultureInfo.CurrentCulture), $"{lowStockProducts.Count} düşük stok"],
                ["Toplam sipariş", orders.Count.ToString("N0", CultureInfo.CurrentCulture), $"{orders.Count(x => x.Status == OrderStatus.Completed)} tamamlandı"],
                ["Toplam müşteri", customers.Count.ToString("N0", CultureInfo.CurrentCulture), "Kayıtlı müşteri"],
                ["Toplam görev", tasks.Count.ToString("N0", CultureInfo.CurrentCulture), $"{tasks.Count(x => x.Status == TaskState.Completed)} tamamlandı"],
                ["Bildirim", notifications.Count.ToString("N0", CultureInfo.CurrentCulture), $"{notifications.Count(x => !x.IsRead)} okunmamış"],
                ["Yorum", reviews.Count.ToString("N0", CultureInfo.CurrentCulture), $"{reviews.Count(x => x.Status == ReviewStatus.Pending)} bekleyen"],
                ["Satış geliri", Money(sales.Sum(x => x.TotalPrice)), $"{sales.Count} satış işlemi"],
                ["Net kâr/zarar", Money(sales.Sum(x => x.ProfitOrLoss)), $"Maliyet {Money(sales.Sum(x => x.TotalCostSnapshot))}"]
            ]);

    private static ReportTableDto BuildProductsTable(IReadOnlyList<Product> products)
        => Table(
            "products",
            "Ürünler",
            "Aktif katalog ve fiyat/stok görünümü.",
            ["Ürün", "Tür", "Stok", "Satış", "Firma", "Eklenme"],
            products.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.Name,
                x.Type.ToString(),
                x.StockQuantity.ToString(CultureInfo.InvariantCulture),
                Money(x.SalePrice),
                x.Company?.Name ?? "-",
                Date(x.CreatedAt)
            ]).ToList());

    private static ReportTableDto BuildStockTable(IReadOnlyList<Product> products)
        => Table(
            "stock",
            "Stok Durumu",
            "Öncelik düşük stokta olacak şekilde stok kontrol listesi.",
            ["Ürün", "Stok", "Alış", "Satış", "Durum"],
            products.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.Name,
                x.StockQuantity.ToString(CultureInfo.InvariantCulture),
                Money(x.PurchasePrice),
                Money(x.SalePrice),
                x.StockQuantity <= 0 ? "Tükendi" : x.StockQuantity <= 5 ? "Düşük" : "Yeterli"
            ]).ToList());

    private static ReportTableDto BuildOrdersTable(IReadOnlyList<Order> orders)
        => Table(
            "orders",
            "Siparişler",
            "Seçili dönem sipariş hareketleri.",
            ["No", "Müşteri", "Toplam", "Ödenen", "Durum", "Ödeme", "Tarih"],
            orders.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.OrderNumber,
                x.Customer.Name,
                Money(x.TotalAmount),
                Money(x.PaidAmount),
                x.Status.ToString(),
                x.PaymentStatus.ToString(),
                Date(x.CreatedAt)
            ]).ToList());

    private static ReportTableDto BuildSalesTable(IReadOnlyList<Transaction> sales)
        => Table(
            "sales",
            "Satışlar",
            "Seçili dönem satış, maliyet ve kâr/zarar tablosu.",
            ["Ürün", "Adet", "Ciro", "Maliyet", "Kâr/Zarar", "Tarih"],
            sales.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.Product?.Name ?? $"Ürün #{x.ProductId}",
                x.Quantity.ToString(CultureInfo.InvariantCulture),
                Money(x.TotalPrice),
                Money(x.TotalCostSnapshot),
                Money(x.ProfitOrLoss),
                Date(x.Date)
            ]).ToList());

    private static ReportTableDto BuildCustomersTable(IReadOnlyList<Customer> customers, IReadOnlyList<Order> orders)
        => Table(
            "customers",
            "Müşteriler",
            "Müşteri listesi ve seçili dönem sipariş hacmi.",
            ["Müşteri", "Telefon", "E-posta", "Sipariş", "Dönem Tutarı"],
            customers.Take(30).Select(customer =>
            {
                var customerOrders = orders.Where(x => x.CustomerId == customer.Id).ToList();
                return (IReadOnlyList<string>)
                [
                    customer.Name,
                    customer.Phone,
                    customer.Email ?? "-",
                    customerOrders.Count.ToString(CultureInfo.InvariantCulture),
                    Money(customerOrders.Sum(x => x.TotalAmount))
                ];
            }).ToList());

    private static ReportTableDto BuildTasksTable(IReadOnlyList<WorkTask> tasks)
        => Table(
            "tasks",
            "Görevler",
            "Operasyon merkezi görev akışı.",
            ["Görev", "Atanan", "Öncelik", "Durum", "Termin", "Oluşturma"],
            tasks.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.Title,
                x.AssignedToUser.FullName,
                x.Priority.ToString(),
                x.Status.ToString(),
                x.DueDate.HasValue ? Date(x.DueDate.Value) : "-",
                Date(x.CreatedAt)
            ]).ToList());

    private static ReportTableDto BuildNotificationsTable(IReadOnlyList<Notification> notifications)
        => Table(
            "notifications",
            "Bildirimler",
            "Sistem içi uyarı ve bilgilendirme akışı.",
            ["Başlık", "Tip", "Okunma", "Kritik", "Tarih"],
            notifications.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.Title,
                x.Type.ToString(),
                x.IsRead ? "Okundu" : "Okunmadı",
                x.IsCritical ? "Evet" : "Hayır",
                Date(x.CreatedAt)
            ]).ToList());

    private static ReportTableDto BuildReviewsTable(IReadOnlyList<ProductReview> reviews)
        => Table(
            "reviews",
            "Yorumlar",
            "Ürün yorumları ve moderasyon durumu.",
            ["Ürün", "Müşteri", "Puan", "Durum", "Yorum", "Tarih"],
            reviews.Take(30).Select(x => (IReadOnlyList<string>)
            [
                x.Product.Name,
                x.Customer?.Name ?? "-",
                x.Rating.ToString(CultureInfo.InvariantCulture),
                x.Status.ToString(),
                Clip(x.Comment, 60),
                Date(x.CreatedAt)
            ]).ToList());

    private static string Money(decimal value)
        => $"{value:N2} TL";

    private static string Date(DateTime value)
        => value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

    private static string Clip(string value, int length)
        => value.Length <= length ? value : $"{value[..Math.Max(0, length - 3)]}...";

    private string BuildReportFileName(OperationalReportDto report)
    {
        var scope = currentUserService.IsInRole(RoleConstants.SystemAdmin)
            ? "platform"
            : $"firma-{currentUserService.CompanyId?.ToString(CultureInfo.InvariantCulture) ?? "0"}";
        return $"MGold-Rapor-{scope}-{report.RangePreset}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
    }

    private string GetArchiveDirectory()
    {
        var scope = currentUserService.IsInRole(RoleConstants.SystemAdmin)
            ? "platform"
            : $"company-{currentUserService.CompanyId?.ToString(CultureInfo.InvariantCulture) ?? "0"}";
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "generated", "reports", scope);
    }

    private byte[] BuildReportPdf(OperationalReportDto report)
    {
        var writer = new PdfDocumentWriter();
        writer.AddLine(_companyProfile.Name, 20);
        writer.AddLine($"{report.ScopeTitle} | {report.CompanyName}", 12);
        writer.AddLine($"Rapor dönemi: {Date(report.StartDate)} - {Date(report.EndDate)}", 10);
        writer.AddLine($"Oluşturma tarihi: {Date(report.GeneratedAt)}", 10);
        writer.AddGap(12);

        writer.AddLine("Özet İstatistikler", 14);
        foreach (var metric in report.Metrics)
        {
            writer.AddLine($"{metric.Label}: {metric.Value} ({metric.Detail})", 10);
        }

        foreach (var table in report.Tables)
        {
            writer.AddGap(14);
            writer.AddLine(table.Title, 14);
            writer.AddLine(table.Description, 9);
            writer.AddLine(string.Join(" | ", table.Columns), 9);
            writer.AddLine(new string('-', 95), 8);

            if (table.Rows.Count == 0)
            {
                writer.AddLine("Kayıt bulunamadı.", 9);
                continue;
            }

            foreach (var row in table.Rows)
            {
                writer.AddWrappedLine(string.Join(" | ", row), 9);
            }
        }

        writer.AddFooter("Bu rapor MGold sistemi tarafından otomatik oluşturulmuştur.");
        return writer.Build();
    }

    private sealed class PdfDocumentWriter
    {
        private readonly List<List<PdfTextLine>> _pages = [[]];
        private int _y = 805;

        public void AddLine(string text, int fontSize = 10)
        {
            EnsureSpace();
            _pages[^1].Add(new PdfTextLine(fontSize, 45, _y, text));
            _y -= Math.Max(fontSize + 7, 16);
        }

        public void AddWrappedLine(string text, int fontSize = 9)
        {
            foreach (var line in Wrap(text, 115))
            {
                AddLine(line, fontSize);
            }
        }

        public void AddGap(int size)
        {
            _y -= size;
            EnsureSpace();
        }

        public void AddFooter(string text)
        {
            for (var index = 0; index < _pages.Count; index++)
            {
                _pages[index].Add(new PdfTextLine(8, 45, 35, $"{text} | Sayfa {index + 1}/{_pages.Count}"));
            }
        }

        public byte[] Build()
        {
            var objects = new List<string>
            {
                "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj"
            };

            var pageObjectIds = new List<int>();
            var nextObjectId = 4;
            foreach (var page in _pages)
            {
                var pageObjectId = nextObjectId++;
                var contentObjectId = nextObjectId++;
                pageObjectIds.Add(pageObjectId);
                var content = BuildContent(page);
                objects.Add($"{pageObjectId} 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >> endobj");
                objects.Add($"{contentObjectId} 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream\nendobj");
            }

            objects.Insert(1, $"2 0 obj << /Type /Pages /Count {_pages.Count} /Kids [{string.Join(' ', pageObjectIds.Select(x => $"{x} 0 R"))}] >> endobj");
            objects.Insert(2, "3 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj");
            objects = objects
                .OrderBy(x => int.Parse(x[..x.IndexOf(' ', StringComparison.Ordinal)], CultureInfo.InvariantCulture))
                .ToList();

            var builder = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int> { 0 };
            foreach (var obj in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
                builder.Append(obj).Append('\n');
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
            builder.Append("0000000000 65535 f \n");
            for (var i = 1; i <= objects.Count; i++)
            {
                builder.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }

            builder.Append("trailer << /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
            builder.Append("startxref\n").Append(xrefOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private void EnsureSpace()
        {
            if (_y >= 70)
            {
                return;
            }

            _pages.Add([]);
            _y = 805;
        }

        private static string BuildContent(IReadOnlyList<PdfTextLine> lines)
        {
            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                builder.AppendLine($"BT /F1 {line.FontSize} Tf {line.X} {line.Y} Td ({Escape(Normalize(line.Text))}) Tj ET");
            }

            return builder.ToString();
        }

        private static IEnumerable<string> Wrap(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                yield return text;
                yield break;
            }

            for (var start = 0; start < text.Length; start += maxLength)
            {
                yield return text.Substring(start, Math.Min(maxLength, text.Length - start));
            }
        }

        private static string Escape(string text)
            => text.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);

        private static string Normalize(string text)
            => text
                .Replace("İ", "I", StringComparison.Ordinal)
                .Replace("ı", "i", StringComparison.Ordinal)
                .Replace("Ş", "S", StringComparison.Ordinal)
                .Replace("ş", "s", StringComparison.Ordinal)
                .Replace("Ğ", "G", StringComparison.Ordinal)
                .Replace("ğ", "g", StringComparison.Ordinal)
                .Replace("Ü", "U", StringComparison.Ordinal)
                .Replace("ü", "u", StringComparison.Ordinal)
                .Replace("Ö", "O", StringComparison.Ordinal)
                .Replace("ö", "o", StringComparison.Ordinal)
                .Replace("Ç", "C", StringComparison.Ordinal)
                .Replace("ç", "c", StringComparison.Ordinal);
    }

    private readonly record struct PdfTextLine(int FontSize, int X, int Y, string Text);
}
