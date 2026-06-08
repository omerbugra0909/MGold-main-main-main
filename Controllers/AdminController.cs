using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Controllers;

[Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
public class AdminController(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IEmailService emailService,
    IOptions<EmailSettings> emailOptions,
    IDashboardService dashboardService,
    IReportService reportService,
    IWorkforceService workforceService,
    IOrderService orderService,
    IInvoiceService invoiceService,
    IOrderHistoryService orderHistoryService,
    IMarketDataService marketDataService) : Controller
{
    private string PanelBasePath => currentUserService.IsInRole(RoleConstants.SystemAdmin) ? "/admin" : "/owner";

    private RedirectResult RedirectToPanel(string path = "")
    {
        var suffix = path.TrimStart('/');
        return Redirect(string.IsNullOrWhiteSpace(suffix) ? PanelBasePath : $"{PanelBasePath}/{suffix}");
    }

    [HttpGet("/admin")]
    [HttpGet("/owner")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyId = currentUserService.CompanyId;
        var scopedUsers = context.AppUsers.AsNoTracking().AsQueryable();
        var scopedOrders = context.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .AsQueryable();
        var scopedProducts = context.Products.AsNoTracking().AsQueryable();
        var scopedSales = context.Transactions
            .AsNoTracking()
            .Where(x => x.Type == TransactionType.Sell)
            .AsQueryable();

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            scopedUsers = scopedUsers.Where(x => x.CompanyId == companyId);
            scopedOrders = scopedOrders.Where(x => x.CompanyId == companyId);
            scopedProducts = scopedProducts.Where(x => x.CompanyId == companyId);
            scopedSales = scopedSales.Where(x => x.CompanyId == companyId);
        }

        var users = await scopedUsers.CountAsync(cancellationToken);
        var orders = await scopedOrders.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var products = await scopedProducts.ToListAsync(cancellationToken);
        var sales = await scopedSales.ToListAsync(cancellationToken);
        var auditQuery = context.AuditLogs.AsNoTracking().AsQueryable();
        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            var companyUsers = context.AppUsers
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .Select(x => x.Id);
            auditQuery = auditQuery.Where(x => x.UserId.HasValue && companyUsers.Contains(x.UserId.Value));
        }

        var recentAuditLogs = await auditQuery
            .OrderByDescending(x => x.Timestamp)
            .Take(8)
            .ToListAsync(cancellationToken);
        var notificationsQuery = await BuildScopedNotificationQueryAsync(cancellationToken);
        var recentNotifications = await notificationsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .ToListAsync(cancellationToken);
        var pendingReviewsQuery = context.ProductReviews
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.Status == ReviewStatus.Pending);
        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            pendingReviewsQuery = pendingReviewsQuery.Where(x => x.Product.CompanyId == companyId);
        }

        var pendingReviews = await pendingReviewsQuery.CountAsync(cancellationToken);
        var market = await marketDataService.GetDashboardAsync("TRY", User.Identity?.Name, cancellationToken);
        var analytics = await dashboardService.GetSummaryAsync(cancellationToken: cancellationToken);

        var model = new AdminDashboardViewModel
        {
            TotalUsers = users,
            TotalOrders = orders.Count,
            TotalRevenue = orders.Sum(x => x.TotalAmount),
            TotalProducts = products.Count,
            UnreadNotifications = recentNotifications.Count(x => !x.IsRead),
            PendingPayments = orders.Count(x => x.PaymentStatus != PaymentStatus.Paid),
            LowStockCount = products.Count(x => x.StockQuantity <= 5),
            NetProfit = sales.Sum(x => x.ProfitOrLoss),
            PendingReviews = pendingReviews,
            RecentOrders = orders.Take(6).Select(x => new AdminDashboardOrderSummary
            {
                OrderId = x.Id,
                OrderNumber = x.OrderNumber,
                CustomerName = x.Customer.Name,
                TotalAmount = x.TotalAmount,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            }).ToList(),
            LowStockProducts = products
                .Where(x => x.StockQuantity <= 5)
                .OrderBy(x => x.StockQuantity)
                .Take(6)
                .Select(x => new AdminLowStockItem
                {
                    ProductId = x.Id,
                    ProductName = x.Name,
                    StockQuantity = x.StockQuantity,
                    SalePrice = x.SalePrice
                }).ToList(),
            RecentActivities = recentAuditLogs.Select(x => new AdminRecentActivityItem
            {
                Title = $"{x.ActionType} / {x.EntityName}",
                Description = string.IsNullOrWhiteSpace(x.Path) ? (x.AfterState ?? "Sistem işlemi") : x.Path,
                Username = x.Username ?? "system",
                CreatedAt = x.Timestamp,
                IsSuccess = x.IsSuccess
            }).ToList(),
            NotificationHighlights = recentNotifications.Select(x => new AdminNotificationHighlight
            {
                Title = x.Title,
                Message = x.Message,
                CreatedAt = x.CreatedAt,
                IsCritical = x.IsCritical
            }).ToList(),
            Analytics = analytics,
            DashboardApiUrl = Url.Action("GetSummary", "Dashboard") ?? "/api/dashboard/summary",
            InitialAnalyticsJson = JsonSerializer.Serialize(analytics, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            MarketHighlights = market.TopMovers.Take(4).Select(x => new AdminMarketHighlight
            {
                Symbol = x.Symbol,
                DisplayName = x.DisplayName,
                Price = x.Price,
                Change24hPercent = x.Change24hPercent,
                DisplayCurrency = x.DisplayCurrency
            }).ToList()
        };

        return View(model);
    }

    [HttpGet("/admin/orders")]
    [HttpGet("/owner/orders")]
    public async Task<IActionResult> Orders(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetAllAsync(cancellationToken);
        if (currentUserService.CompanyId.HasValue && !currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            orders = orders.Where(x => x.CompanyId == currentUserService.CompanyId).ToList();
        }
        var model = new AdminOrdersPageViewModel
        {
            Orders = orders,
            TotalRevenue = orders.Sum(x => x.TotalAmount),
            CollectedRevenue = orders.Sum(x => x.PaidAmount)
        };

        return View(model);
    }

    [HttpGet("/admin/orders/{id:int}")]
    [HttpGet("/owner/orders/{id:int}")]
    public async Task<IActionResult> OrderDetails(int id, CancellationToken cancellationToken)
    {
        var model = await orderService.GetByIdAsync(id, cancellationToken);
        return View(model);
    }

    [HttpGet("/admin/invoices/{invoiceId:int}/download")]
    [HttpGet("/owner/invoices/{invoiceId:int}/download")]
    public async Task<IActionResult> DownloadInvoice(int invoiceId, CancellationToken cancellationToken)
    {
        var pdf = await invoiceService.GetPdfAsync(invoiceId, cancellationToken);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    [HttpGet("/admin/invoices/{invoiceId:int}/preview")]
    [HttpGet("/owner/invoices/{invoiceId:int}/preview")]
    public async Task<IActionResult> PreviewInvoice(int invoiceId, CancellationToken cancellationToken)
    {
        var pdf = await invoiceService.GetPdfAsync(invoiceId, cancellationToken);
        return File(pdf.Content, "application/pdf");
    }

    [HttpGet("/admin/products")]
    [HttpGet("/owner/products")]
    public async Task<IActionResult> Products(CancellationToken cancellationToken)
    {
        var companyId = currentUserService.CompanyId;
        var model = new AdminProductsPageViewModel
        {
            Products = await context.Products
                .AsNoTracking()
                .Include(x => x.Company)
                .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.CompanyId == companyId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            Companies = currentUserService.IsInRole(RoleConstants.SystemAdmin)
                ? await context.Companies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken)
                : []
        };

        return View(model);
    }

    [HttpGet("/admin/reviews")]
    [HttpGet("/owner/reviews")]
    public async Task<IActionResult> Reviews(CancellationToken cancellationToken)
    {
        var companyId = currentUserService.CompanyId;
        var model = await context.ProductReviews
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.Product.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(model);
    }

    [HttpGet("/admin/notifications")]
    [HttpGet("/owner/notifications")]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        var notificationsQuery = await BuildScopedNotificationQueryAsync(cancellationToken);
        var model = await notificationsQuery
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(model);
    }

    [HttpGet("/admin/reports")]
    [HttpGet("/owner/reports")]
    public async Task<IActionResult> Reports(string? rangePreset, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var model = new AdminReportsPageViewModel
        {
            RangePreset = rangePreset ?? DashboardRangePresets.Monthly,
            StartDate = startDate,
            EndDate = endDate,
            Summary = await reportService.GetProfitLossSummaryAsync(startDate, endDate, cancellationToken),
            OperationalReport = await reportService.GetOperationalReportAsync(rangePreset, startDate, endDate, cancellationToken),
            Archive = await reportService.GetArchivedReportsAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpGet("/admin/reports/pdf")]
    [HttpGet("/owner/reports/pdf")]
    public async Task<IActionResult> DownloadOperationalReportPdf(
        string? rangePreset,
        DateTime? startDate,
        DateTime? endDate,
        bool archive = false,
        CancellationToken cancellationToken = default)
    {
        var pdf = await reportService.GenerateOperationalReportPdfAsync(rangePreset, startDate, endDate, archive, cancellationToken);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    [HttpGet("/admin/reports/archive/{fileName}")]
    [HttpGet("/owner/reports/archive/{fileName}")]
    public async Task<IActionResult> DownloadArchivedReport(string fileName, CancellationToken cancellationToken)
    {
        var pdf = await reportService.GetArchivedReportAsync(fileName, cancellationToken);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    [HttpGet("/admin/users")]
    [HttpGet("/owner/users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var companyId = currentUserService.CompanyId;
        var model = await context.AppUsers
            .AsNoTracking()
            .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.CompanyId == companyId)
            .Include(x => x.Customer)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(model);
    }

    [HttpGet("/admin/company/{companyId:int}")]
    [HttpGet("/owner/company")]
    public async Task<IActionResult> CompanyProfile(int? companyId, CancellationToken cancellationToken)
    {
        var resolvedCompanyId = currentUserService.IsInRole(RoleConstants.SystemAdmin)
            ? companyId
            : currentUserService.CompanyId;
        if (!resolvedCompanyId.HasValue)
        {
            TempData["Error"] = "Firma bağlamı bulunamadı.";
            return RedirectToPanel();
        }

        var company = await context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == resolvedCompanyId.Value, cancellationToken);
        if (company is null)
        {
            TempData["Error"] = "Firma bulunamadı.";
            return RedirectToPanel();
        }

        return View("~/Views/Admin/CompanyProfile.cshtml", new AdminCompanyProfilePageViewModel
        {
            CompanyId = company.Id,
            Form = new UpdateCompanyProfileDto
            {
                Name = company.Name,
                Code = company.Code,
                Address = company.Address,
                City = company.City,
                District = company.District,
                Description = company.Description,
                LogoUrl = company.LogoUrl,
                CoverImageUrl = company.CoverImageUrl,
                ContactEmail = company.ContactEmail,
                ContactPhone = company.ContactPhone,
                WebsiteUrl = company.WebsiteUrl,
                TaxOffice = company.TaxOffice,
                TaxNumber = company.TaxNumber,
                SocialLinks = company.SocialLinks,
                WorkingHours = company.WorkingHours,
                Categories = company.Categories,
                SearchKeywords = company.SearchKeywords,
                IsActive = company.IsActive
            },
            CategoryOptions = CompanyCategoryOptions.All
        });
    }

    [HttpPost("/admin/company/{companyId:int}")]
    [HttpPost("/owner/company")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCompanyProfile(int? companyId, UpdateCompanyProfileDto form, CancellationToken cancellationToken)
    {
        var resolvedCompanyId = currentUserService.IsInRole(RoleConstants.SystemAdmin)
            ? companyId
            : currentUserService.CompanyId;
        if (!resolvedCompanyId.HasValue)
        {
            TempData["Error"] = "Firma bağlamı bulunamadı.";
            return RedirectToPanel();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Firma bilgilerini kontrol edin.";
            return RedirectToAction(nameof(CompanyProfile), currentUserService.IsInRole(RoleConstants.SystemAdmin) ? new { companyId = resolvedCompanyId.Value } : null);
        }

        await workforceService.UpdateCompanyProfileAsync(resolvedCompanyId.Value, form, cancellationToken);
        TempData["Success"] = "Firma profili güncellendi.";
        return currentUserService.IsInRole(RoleConstants.SystemAdmin)
            ? Redirect($"/admin/company/{resolvedCompanyId.Value}")
            : Redirect("/owner/company");
    }

    [HttpPost("/admin/orders/status")]
    [HttpPost("/owner/orders/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, OrderStatus status, string? notes, CancellationToken cancellationToken)
    {
        var updatedOrder = await orderService.UpdateStatusAsync(id, new UpdateOrderStatusDto
        {
            Status = status,
            Notes = notes
        }, cancellationToken);

        await orderHistoryService.RecordAsync(
            id,
            OrderHistoryType.AdminAction,
            "Admin sipariş durumu güncelledi",
            $"{updatedOrder.OrderNumber} için panel üzerinden yeni durum {status} olarak kaydedildi.",
            cancellationToken: cancellationToken);

        await NotifyAdminAsync(
            subject: $"Sipariş durumu güncellendi: {updatedOrder.OrderNumber}",
            htmlBody: $"""
                <h2>Sipariş durum degisikligi</h2>
                <p><strong>Sipariş No:</strong> {updatedOrder.OrderNumber}</p>
                <p><strong>Müşteri:</strong> {updatedOrder.CustomerName}</p>
                <p><strong>Yeni Durum:</strong> {updatedOrder.Status}</p>
                <p><strong>Güncellenme:</strong> {updatedOrder.UpdatedAt.ToLocalTime():dd.MM.yyyy HH:mm}</p>
            """,
            cancellationToken);

        return RedirectToPanel($"orders/{id}");
    }

    [HttpPost("/admin/orders/payment")]
    [HttpPost("/owner/orders/payment")]
    [EnableRateLimiting("payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOrderPayment(int id, AdminOrderPaymentForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Ödeme bilgilerini kontrol edin.";
            return RedirectToPanel($"orders/{id}");
        }

        var updatedOrder = await orderService.AddPaymentAsync(id, new CreateOrderPaymentDto
        {
            Method = form.Method,
            Status = form.Status,
            Amount = form.Amount,
            ReferenceNumber = form.ReferenceNumber,
            ProviderKey = form.ProviderKey,
            ProviderTransactionId = form.ProviderTransactionId,
            IdempotencyKey = form.IdempotencyKey,
            InstallmentCount = form.InstallmentCount,
            RequiresThreeDSecure = form.RequiresThreeDSecure,
            ThreeDSecureStatus = form.ThreeDSecureStatus,
            ParentPaymentId = form.ParentPaymentId,
            IsRefund = form.IsRefund,
            FailureCode = form.FailureCode,
            FailureMessage = form.FailureMessage,
            Notes = form.Notes,
            PaidAt = form.PaidAt
        }, cancellationToken);

        await orderHistoryService.RecordAsync(
            id,
            OrderHistoryType.AdminAction,
            "Admin ödeme ekledi",
            $"{form.Amount:N2} TL tutarındaki ödeme admin panelinden kaydedildi.",
            cancellationToken: cancellationToken);

        TempData["Success"] = $"{updatedOrder.OrderNumber} için ödeme kaydı eklendi.";
        return RedirectToPanel($"orders/{id}");
    }

    [HttpPost("/admin/orders/invoice")]
    [HttpPost("/owner/orders/invoice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateInvoice(int id, CancellationToken cancellationToken)
    {
        var invoice = await invoiceService.GenerateForOrderAsync(id, cancellationToken);
        await orderHistoryService.RecordAsync(
            id,
            OrderHistoryType.AdminAction,
            "Admin fatura oluşturdu",
            $"{invoice.InvoiceNumber} numaralı fatura admin panelinden oluşturuldu.",
            cancellationToken: cancellationToken);

        TempData["Success"] = $"Fatura hazır: {invoice.InvoiceNumber}";
        return RedirectToPanel($"orders/{id}");
    }

    [HttpPost("/admin/reviews/moderate")]
    [HttpPost("/owner/reviews/moderate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateReview(int id, ReviewStatus status, string? adminReply, CancellationToken cancellationToken)
    {
        var review = await context.ProductReviews.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (review is not null)
        {
            var productCompanyId = await context.Products
                .AsNoTracking()
                .Where(x => x.Id == review.ProductId)
                .Select(x => x.CompanyId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && productCompanyId != currentUserService.CompanyId)
            {
                TempData["Error"] = "Bu yorumu güncelleme yetkiniz yok.";
                return RedirectToPanel("reviews");
            }

            if (!Enum.IsDefined(status))
            {
                TempData["Error"] = "Geçersiz yorum durumu.";
                return RedirectToPanel("reviews");
            }

            review.Status = status;
            review.AdminReply = string.IsNullOrWhiteSpace(adminReply) ? null : adminReply.Trim();
            review.ModeratedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToPanel("reviews");
    }

    [HttpPost("/admin/notifications/read")]
    [HttpPost("/owner/notifications/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(int id, CancellationToken cancellationToken)
    {
        var notification = await context.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is not null)
        {
            if (!await CanAccessNotificationAsync(notification, cancellationToken))
            {
                TempData["Error"] = "Bu bildirimi güncelleme yetkiniz yok.";
                return RedirectToPanel("notifications");
            }

            notification.IsRead = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToPanel("notifications");
    }

    [HttpPost("/admin/users/update")]
    [HttpPost("/owner/users/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(int id, string role, bool isActive, CancellationToken cancellationToken)
    {
        var user = await context.AppUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return RedirectToPanel("users");
        }

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && user.CompanyId != currentUserService.CompanyId)
        {
            TempData["Error"] = "Bu kullanıcıyi güncelleme yetkiniz yok.";
            return RedirectToPanel("users");
        }

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin)
            && string.Equals(role, RoleConstants.SystemAdmin, StringComparison.Ordinal))
        {
            TempData["Error"] = "Sistem admin rolu yalnızca sistem admini tarafından atanabilir.";
            return RedirectToPanel("users");
        }

        if (string.Equals(User.Identity?.Name, user.Username, StringComparison.OrdinalIgnoreCase) && !isActive)
        {
            TempData["Error"] = "Kendi admin hesabınızı pasife alamazsiniz.";
            return RedirectToPanel("users");
        }

        if (!RoleConstants.All.Contains(role))
        {
            TempData["Error"] = "Geçersiz rol secimi.";
            return RedirectToPanel("users");
        }

        user.Role = role;
        user.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        await NotifyAdminAsync(
            subject: $"Kullanıcı hesabı güncellendi: {user.Username}",
            htmlBody: $"""
                <h2>Onemli sistem bildirimi</h2>
                <p><strong>Kullanıcı:</strong> {user.FullName} ({user.Username})</p>
                <p><strong>Yeni Rol:</strong> {user.Role}</p>
                <p><strong>Durum:</strong> {(user.IsActive ? "Aktif" : "Pasif")}</p>
            """,
            cancellationToken);
        TempData["Success"] = $"Kullanıcı güncellendi: {user.Username}";
        return RedirectToPanel("users");
    }

    [HttpPost("/admin/products/create")]
    [HttpPost("/owner/products/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(AdminProductForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Ürün bilgilerini kontrol edin.";
            return RedirectToPanel("products");
        }

        var productCompanyId = currentUserService.CompanyId;
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            if (!form.CompanyId.HasValue)
            {
                TempData["Error"] = "Sistem admini ürün eklerken firma secmelidir. Ürünler global olamaz.";
                return RedirectToPanel("products");
            }

            var companyExists = await context.Companies
                .AnyAsync(x => x.Id == form.CompanyId.Value && x.IsActive, cancellationToken);
            if (!companyExists)
            {
                TempData["Error"] = "Secilen firma bulunamadı veya pasif.";
                return RedirectToPanel("products");
            }

            productCompanyId = form.CompanyId.Value;
        }

        if (!productCompanyId.HasValue)
        {
            TempData["Error"] = "Ürün eklemek için firma bağlami zorunludur.";
            return RedirectToPanel("products");
        }

        await context.Products.AddAsync(new Product
        {
            Name = form.Name.Trim(),
            Type = form.Type,
            Weight = form.Weight,
            PurityRate = form.PurityRate,
            LaborCost = form.LaborCost,
            LaborCostPercentage = form.LaborCostPercentage,
            AdditionalCost = form.AdditionalCost,
            ProfitMarginPercentage = form.ProfitMarginPercentage,
            PurchasePrice = form.PurchasePrice,
            SalePrice = form.SalePrice,
            StockQuantity = form.StockQuantity,
            CompanyId = productCompanyId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"Ürün eklendi: {form.Name}";
        return RedirectToPanel("products");
    }

    [HttpPost("/admin/products/update")]
    [HttpPost("/owner/products/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProduct(AdminProductForm form, CancellationToken cancellationToken)
    {
        if (form.Id <= 0)
        {
            TempData["Error"] = "Geçerli ürün secilmelidir.";
            return RedirectToPanel("products");
        }

        var product = await context.Products.FirstOrDefaultAsync(x => x.Id == form.Id, cancellationToken);
        if (product is null)
        {
            TempData["Error"] = "Ürün bulunamadı.";
            return RedirectToPanel("products");
        }

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && product.CompanyId != currentUserService.CompanyId)
        {
            TempData["Error"] = "Bu ürünu güncelleme yetkiniz yok.";
            return RedirectToPanel("products");
        }

        product.Name = form.Name.Trim();
        product.Type = form.Type;
        product.Weight = form.Weight;
        product.PurityRate = form.PurityRate;
        product.LaborCost = form.LaborCost;
        product.LaborCostPercentage = form.LaborCostPercentage;
        product.AdditionalCost = form.AdditionalCost;
        product.ProfitMarginPercentage = form.ProfitMarginPercentage;
        product.PurchasePrice = form.PurchasePrice;
        product.SalePrice = form.SalePrice;
        product.StockQuantity = form.StockQuantity;

        await context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"Ürün güncellendi: {product.Name}";
        return RedirectToPanel("products");
    }

    [HttpPost("/admin/products/delete")]
    [HttpPost("/owner/products/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
    {
        var product = await context.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            TempData["Error"] = "Silinecek ürün bulunamadı.";
            return RedirectToPanel("products");
        }

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && product.CompanyId != currentUserService.CompanyId)
        {
            TempData["Error"] = "Bu ürünu silme yetkiniz yok.";
            return RedirectToPanel("products");
        }

        var hasOrders = await context.OrderItems.AnyAsync(x => x.ProductId == id, cancellationToken);
        var hasTransactions = await context.Transactions.AnyAsync(x => x.ProductId == id, cancellationToken);
        if (hasOrders || hasTransactions)
        {
            TempData["Error"] = "Sipariş veya işlem geçmişi olan ürün silinemez.";
            return RedirectToPanel("products");
        }

        context.Products.Remove(product);
        await context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"Ürün silindi: {product.Name}";
        return RedirectToPanel("products");
    }

    private async Task NotifyAdminAsync(string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var supportInbox = emailOptions.Value.SupportInbox;
        if (string.IsNullOrWhiteSpace(supportInbox))
        {
            return;
        }

        await emailService.SendAsync(new SendEmailRequestDto
        {
            To = supportInbox,
            Subject = subject,
            HtmlBody = htmlBody
        }, cancellationToken);
    }

    private async Task<IQueryable<Notification>> BuildScopedNotificationQueryAsync(CancellationToken cancellationToken)
    {
        var query = context.Notifications.AsNoTracking().AsQueryable();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            return query;
        }

        query = query.Where(x =>
            x.TargetRole == null ||
            x.TargetRole == RoleConstants.Manager ||
            x.TargetRole == RoleConstants.ManagerOnly);

        if (currentUserService.CompanyId is not int companyId)
        {
            return query.Where(x => x.RelatedEntityName == null);
        }

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

    private async Task<bool> CanAccessNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            return true;
        }

        if (notification.TargetRole is not null
            && notification.TargetRole != RoleConstants.Manager
            && notification.TargetRole != RoleConstants.ManagerOnly)
        {
            return false;
        }

        if (currentUserService.CompanyId is not int companyId)
        {
            return string.IsNullOrWhiteSpace(notification.RelatedEntityName);
        }

        if (string.IsNullOrWhiteSpace(notification.RelatedEntityName) || string.IsNullOrWhiteSpace(notification.RelatedEntityId))
        {
            return true;
        }

        if (!int.TryParse(notification.RelatedEntityId, out var relatedId))
        {
            return false;
        }

        return notification.RelatedEntityName switch
        {
            nameof(Order) => await context.Orders.AnyAsync(x => x.Id == relatedId && x.CompanyId == companyId, cancellationToken),
            nameof(Product) => await context.Products.AnyAsync(x => x.Id == relatedId && x.CompanyId == companyId, cancellationToken),
            nameof(ProductReview) => await context.ProductReviews.AnyAsync(x => x.Id == relatedId && x.Product.CompanyId == companyId, cancellationToken),
            _ => false
        };
    }
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalProducts { get; set; }
    public int UnreadNotifications { get; set; }
    public int PendingPayments { get; set; }
    public int LowStockCount { get; set; }
    public int PendingReviews { get; set; }
    public decimal NetProfit { get; set; }
    public IReadOnlyList<AdminDashboardOrderSummary> RecentOrders { get; set; } = [];
    public IReadOnlyList<AdminLowStockItem> LowStockProducts { get; set; } = [];
    public IReadOnlyList<AdminRecentActivityItem> RecentActivities { get; set; } = [];
    public IReadOnlyList<AdminNotificationHighlight> NotificationHighlights { get; set; } = [];
    public IReadOnlyList<AdminMarketHighlight> MarketHighlights { get; set; } = [];
    public DashboardSummaryDto Analytics { get; set; } = new();
    public string DashboardApiUrl { get; set; } = string.Empty;
    public string InitialAnalyticsJson { get; set; } = "{}";
}

public class AdminMarketHighlight
{
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Change24hPercent { get; set; }
    public string DisplayCurrency { get; set; } = "TRY";
}

public class AdminDashboardOrderSummary
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminLowStockItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public decimal SalePrice { get; set; }
}

public class AdminRecentActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsSuccess { get; set; }
}

public class AdminNotificationHighlight
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsCritical { get; set; }
}

public class AdminOrdersPageViewModel
{
    public IReadOnlyList<OrderDto> Orders { get; set; } = [];
    public decimal TotalRevenue { get; set; }
    public decimal CollectedRevenue { get; set; }
}

public class AdminReportsPageViewModel
{
    public string RangePreset { get; set; } = DashboardRangePresets.Monthly;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProfitLossSummaryDto Summary { get; set; } = new();
    public OperationalReportDto OperationalReport { get; set; } = new();
    public IReadOnlyList<ReportArchiveItemDto> Archive { get; set; } = [];
}

public class AdminProductsPageViewModel
{
    public IReadOnlyList<Product> Products { get; set; } = [];
    public IReadOnlyList<Company> Companies { get; set; } = [];
}

public class AdminCompanyProfilePageViewModel
{
    public int CompanyId { get; set; }
    public UpdateCompanyProfileDto Form { get; set; } = new();
    public IReadOnlyList<string> CategoryOptions { get; set; } = [];
}

public class AdminOrderPaymentForm
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public PaymentStatus Status { get; set; } = PaymentStatus.Paid;
    [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string? ReferenceNumber { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(80)]
    public string? ProviderKey { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(160)]
    public string? ProviderTransactionId { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    public string? IdempotencyKey { get; set; }
    [System.ComponentModel.DataAnnotations.Range(1, 36)]
    public int InstallmentCount { get; set; } = 1;
    public bool RequiresThreeDSecure { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(40)]
    public string? ThreeDSecureStatus { get; set; }
    public int? ParentPaymentId { get; set; }
    public bool IsRefund { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(80)]
    public string? FailureCode { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? FailureMessage { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class AdminProductForm
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@".*\S.*")]
    public string Name { get; set; } = string.Empty;
    public ProductType Type { get; set; } = ProductType.Gold;
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal Weight { get; set; }
    [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0", "1")]
    public decimal PurityRate { get; set; } = 1m;
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal LaborCost { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal LaborCostPercentage { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal AdditionalCost { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal ProfitMarginPercentage { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, double.MaxValue)]
    public decimal SalePrice { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
}
