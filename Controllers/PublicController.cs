using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.DTOs;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Controllers;

public class PublicController(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IEmailService emailService,
    INotificationService notificationService,
    IOptions<EmailSettings> emailOptions) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index([FromQuery] PublicProductFilterForm filter, CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildPageModelAsync(cancellationToken, filterOverride: filter));
        }
        catch (Exception ex)
        {
            ViewData["LoadError"] = $"Urunler yuklenemedi: {ex.Message}";
            return View(new PublicProductsPageViewModel());
        }
    }

    [AllowAnonymous]
    public async Task<IActionResult> Products([FromQuery] PublicProductFilterForm filter, CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildPageModelAsync(cancellationToken, filterOverride: filter));
        }
        catch (Exception ex)
        {
            ViewData["LoadError"] = $"Urunler yuklenemedi: {ex.Message}";
            return View(new PublicProductsPageViewModel());
        }
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = nameof(PublicProductsPageViewModel.CreateForm))] PublicProductForm form,
        CancellationToken cancellationToken)
    {
        ValidateProductForm(form);
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(cancellationToken, createFormOverride: form));
        }

        try
        {
            if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
            {
                TempData["Error"] = "Sistem admini urun eklerken firma baglami secmelidir. Urunler global olamaz.";
                return RedirectToAction(nameof(Index));
            }

            if (!currentUserService.CompanyId.HasValue)
            {
                TempData["Error"] = "Urun eklemek icin firma baglami zorunludur.";
                return RedirectToAction(nameof(Index));
            }

            var entity = MapToEntity(form);
            entity.CompanyId = currentUserService.CompanyId;

            await context.Products.AddAsync(entity, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = $"Urun eklendi: {entity.Name}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ViewData["LoadError"] = GetFriendlyMessage(ex, "Urun eklenemedi.");
            return View("Index", await BuildPageModelAsync(cancellationToken, createFormOverride: form));
        }
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        [Bind(Prefix = nameof(PublicProductsPageViewModel.EditForm))] PublicProductEditForm form,
        CancellationToken cancellationToken)
    {
        ValidateEditProductForm(form);
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(cancellationToken, editFormOverride: form, openEditPanel: true));
        }

        try
        {
            var entity = await context.Products.FirstOrDefaultAsync(x => x.Id == form.Id, cancellationToken);
            if (entity is null)
            {
                TempData["Error"] = "Guncellenecek urun bulunamadi.";
                return RedirectToAction(nameof(Index));
            }

            if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && entity.CompanyId != currentUserService.CompanyId)
            {
                TempData["Error"] = "Bu urunu guncelleme yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            MapToExistingEntity(entity, form);
            await context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = $"Urun guncellendi: {entity.Name}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ViewData["LoadError"] = GetFriendlyMessage(ex, "Urun guncellenemedi.");
            return View("Index", await BuildPageModelAsync(cancellationToken, editFormOverride: form, openEditPanel: true));
        }
    }

    [HttpPost]
    [Authorize(Roles = RoleConstants.CustomerOnly)]
    [EnableRateLimiting("payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(
        int productId,
        int quantity,
        PaymentMethod preferredPaymentMethod,
        string? notes,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            TempData["Error"] = "Siparis miktari en az 1 olmalidir.";
            return RedirectToLocal(returnUrl);
        }

        if (!Enum.IsDefined(preferredPaymentMethod))
        {
            TempData["Error"] = "Gecerli bir odeme tercihi seciniz.";
            return RedirectToLocal(returnUrl);
        }

        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            TempData["Error"] = "Urun siparisi icin giris yapmaniz gerekmektedir.";
            return RedirectToLocal(returnUrl);
        }

        var user = await context.AppUsers
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user?.CustomerId is null)
        {
            TempData["Error"] = "Siparis vermek icin once hesap bilgilerinizi tamamlayin.";
            return RedirectToLocal(returnUrl);
        }

        await using var dbTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var product = await context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
        {
            TempData["Error"] = "Siparis verilecek urun bulunamadi.";
            return RedirectToLocal(returnUrl);
        }

        if (user.CompanyId.HasValue && product.CompanyId.HasValue && user.CompanyId != product.CompanyId)
        {
            TempData["Error"] = "Bu urun icin siparis olusturma yetkiniz yok.";
            return RedirectToLocal(returnUrl);
        }

        if (product.StockQuantity < quantity)
        {
            TempData["Error"] = "Yeterli stok bulunmuyor.";
            return RedirectToLocal(returnUrl);
        }

        var totalAmount = product.SalePrice * quantity;
        var order = new Order
        {
            CustomerId = user.CustomerId.Value,
            OrderNumber = CreateOrderNumber(),
            CompanyId = product.CompanyId ?? user.CompanyId,
            Status = OrderStatus.Preparing,
            Notes = string.IsNullOrWhiteSpace(notes) ? $"Web siparisi: {product.Name}" : notes.Trim(),
            PreferredPaymentMethod = preferredPaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TotalAmount = totalAmount,
            Items =
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.SalePrice,
                    TotalPrice = totalAmount,
                    Notes = "Dashboard urun siparisi"
                }
            }
        };

        product.StockQuantity -= quantity;
        await context.Orders.AddAsync(order, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.Notifications.AddAsync(new Notification
        {
            Title = "Yeni urun siparisi",
            Message = $"{user.FullName} tarafindan {product.Name} icin siparis olusturuldu.",
            Type = NotificationType.Info,
            TargetRole = RoleConstants.ManagerOnly,
            RelatedEntityName = nameof(Order),
            RelatedEntityId = order.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        await NotifyAdminAsync(
            subject: $"Yeni siparis alindi: {order.OrderNumber}",
            htmlBody: $"""
                <h2>Yeni siparis bildirimi</h2>
                <p><strong>Siparis No:</strong> {order.OrderNumber}</p>
                <p><strong>Kullanici:</strong> {user.FullName} ({user.Email})</p>
                <p><strong>Urun:</strong> {product.Name}</p>
                <p><strong>Miktar:</strong> {quantity}</p>
                <p><strong>Toplam:</strong> {totalAmount:N2} TL</p>
                <p><strong>Odeme Tercihi:</strong> {preferredPaymentMethod}</p>
                <p><strong>Not:</strong> {order.Notes}</p>
            """,
            cancellationToken);
        TempData["Success"] = $"Siparisiniz alindi: {product.Name}";
        return RedirectToLocal(returnUrl);
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int id, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await context.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                TempData["Error"] = "Silinecek urun bulunamadi.";
                return RedirectToAction(nameof(Index));
            }

            if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && entity.CompanyId != currentUserService.CompanyId)
            {
                TempData["Error"] = "Bu urunu silme yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            var hasOrders = await context.OrderItems.AnyAsync(x => x.ProductId == id, cancellationToken);
            var hasTransactions = await context.Transactions.AnyAsync(x => x.ProductId == id, cancellationToken);
            if (hasOrders || hasTransactions)
            {
                TempData["Error"] = "Siparis veya islem gecmisi olan urun silinemez.";
                return RedirectToAction(nameof(Index));
            }

            context.Products.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);

            TempData["Success"] = $"Urun silindi: {entity.Name}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = GetFriendlyMessage(ex, "Urun silinemedi.");
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<PublicProductsPageViewModel> BuildPageModelAsync(
        CancellationToken cancellationToken,
        PublicProductForm? createFormOverride = null,
        PublicProductEditForm? editFormOverride = null,
        bool openEditPanel = false,
        PublicProductFilterForm? filterOverride = null)
    {
        var filter = filterOverride ?? new PublicProductFilterForm();
        var productQuery = context.Products
            .AsNoTracking()
            .AsQueryable();
        var canSeeOperations = currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || currentUserService.IsInRole(RoleConstants.Manager);

        if (currentUserService.IsInRole(RoleConstants.Manager) && !currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            productQuery = productQuery.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            productQuery = productQuery.Where(x => x.Name.ToLower().Contains(search));
        }

        if (filter.Type.HasValue)
        {
            productQuery = productQuery.Where(x => x.Type == filter.Type.Value);
        }

        if (filter.MinPrice.HasValue)
        {
            productQuery = productQuery.Where(x => x.SalePrice >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            productQuery = productQuery.Where(x => x.SalePrice <= filter.MaxPrice.Value);
        }

        if (filter.InStockOnly)
        {
            productQuery = productQuery.Where(x => x.StockQuantity > 0);
        }

        if (filter.LowStockOnly)
        {
            productQuery = productQuery.Where(x => x.StockQuantity <= 5);
        }

        var products = await productQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .Select(x => new PublicProductViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                StockQuantity = x.StockQuantity,
                Weight = x.Weight,
                PurityRate = x.PurityRate,
                LaborCost = x.LaborCost,
                LaborCostPercentage = x.LaborCostPercentage,
                PurchasePrice = x.PurchasePrice,
                SalePrice = x.SalePrice,
                AdditionalCost = x.AdditionalCost,
                ProfitMarginPercentage = x.ProfitMarginPercentage,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var allProductsQuery = context.Products.AsNoTracking().AsQueryable();
        if (currentUserService.IsInRole(RoleConstants.Manager) && !currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            allProductsQuery = allProductsQuery.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        var allProducts = await allProductsQuery.ToListAsync(cancellationToken);
        var recentOrders = canSeeOperations
            ? await context.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.CompanyId == currentUserService.CompanyId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new PublicOrderSummaryViewModel
            {
                OrderNumber = x.OrderNumber,
                CustomerName = x.Customer.Name,
                Status = x.Status,
                TotalAmount = x.TotalAmount,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken)
            : [];

        var notifications = canSeeOperations
            ? (await notificationService.GetForCurrentUserAsync(unreadOnly: false, cancellationToken))
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(x => new PublicNotificationViewModel
                {
                    Title = x.Title,
                    Message = x.Message,
                    Type = x.Type,
                    CreatedAt = x.CreatedAt
                })
                .ToList()
            : [];

        var reviews = await context.ProductReviews
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.Status == ReviewStatus.Approved)
            .Where(x => !currentUserService.IsInRole(RoleConstants.Manager)
                || currentUserService.IsInRole(RoleConstants.SystemAdmin)
                || x.Product.CompanyId == currentUserService.CompanyId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new PublicReviewSummaryViewModel
            {
                ProductName = x.Product.Name,
                Rating = x.Rating,
                Comment = x.Comment,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orders = canSeeOperations
            ? await context.Orders.AsNoTracking()
                .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.CompanyId == currentUserService.CompanyId)
                .ToListAsync(cancellationToken)
            : [];
        var sales = canSeeOperations
            ? await context.Transactions.AsNoTracking()
            .Where(x => x.Type == TransactionType.Sell)
            .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.CompanyId == currentUserService.CompanyId)
            .ToListAsync(cancellationToken)
            : [];

        var pageModel = new PublicProductsPageViewModel
        {
            Products = products,
            CreateForm = createFormOverride ?? new PublicProductForm(),
            EditForm = editFormOverride ?? new PublicProductEditForm(),
            Filter = filter,
            IsEditPanelOpen = openEditPanel,
            RecentOrders = recentOrders,
            Notifications = notifications,
            RecentReviews = reviews,
            Dashboard = new PublicDashboardViewModel
            {
                TotalProducts = allProducts.Count,
                TotalStock = allProducts.Sum(x => x.StockQuantity),
                TotalRevenue = canSeeOperations ? sales.Sum(x => x.TotalPrice) : 0m,
                NetProfit = canSeeOperations ? sales.Sum(x => x.ProfitOrLoss) : 0m,
                OpenOrders = canSeeOperations ? orders.Count(x => x.Status is OrderStatus.Preparing or OrderStatus.Ready) : 0,
                LowStockCount = allProducts.Count(x => x.StockQuantity <= 5)
            }
        };

        if (!openEditPanel || editFormOverride?.Id is null or <= 0)
        {
            return pageModel;
        }

        return pageModel;
    }

    private static Product MapToEntity(PublicProductForm form)
        => new()
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
            StockQuantity = form.StockQuantity
        };

    private static string CreateOrderNumber()
        => $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();

    private static void MapToExistingEntity(Product entity, PublicProductForm form)
    {
        entity.Name = form.Name.Trim();
        entity.Type = form.Type;
        entity.Weight = form.Weight;
        entity.PurityRate = form.PurityRate;
        entity.LaborCost = form.LaborCost;
        entity.LaborCostPercentage = form.LaborCostPercentage;
        entity.AdditionalCost = form.AdditionalCost;
        entity.ProfitMarginPercentage = form.ProfitMarginPercentage;
        entity.PurchasePrice = form.PurchasePrice;
        entity.SalePrice = form.SalePrice;
        entity.StockQuantity = form.StockQuantity;
    }

    private void ValidateProductForm(PublicProductForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            ModelState.AddModelError($"{nameof(PublicProductsPageViewModel.CreateForm)}.{nameof(PublicProductForm.Name)}", "Urun adi zorunludur.");
        }
    }

    private void ValidateEditProductForm(PublicProductEditForm form)
    {
        if (form.Id <= 0)
        {
            ModelState.AddModelError($"{nameof(PublicProductsPageViewModel.EditForm)}.{nameof(PublicProductEditForm.Id)}", "Gecerli urun secilmelidir.");
        }

        if (string.IsNullOrWhiteSpace(form.Name))
        {
            ModelState.AddModelError($"{nameof(PublicProductsPageViewModel.EditForm)}.{nameof(PublicProductEditForm.Name)}", "Urun adi zorunludur.");
        }
    }

    private static string GetFriendlyMessage(Exception exception, string fallbackMessage)
        => exception switch
        {
            BusinessRuleException => exception.Message,
            DbUpdateException => fallbackMessage,
            _ => fallbackMessage
        };

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

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}

public class PublicProductsPageViewModel
{
    public IReadOnlyList<PublicProductViewModel> Products { get; set; } = [];
    public PublicProductForm CreateForm { get; set; } = new();
    public PublicProductEditForm EditForm { get; set; } = new();
    public PublicProductFilterForm Filter { get; set; } = new();
    public bool IsEditPanelOpen { get; set; }
    public PublicDashboardViewModel Dashboard { get; set; } = new();
    public IReadOnlyList<PublicOrderSummaryViewModel> RecentOrders { get; set; } = [];
    public IReadOnlyList<PublicNotificationViewModel> Notifications { get; set; } = [];
    public IReadOnlyList<PublicReviewSummaryViewModel> RecentReviews { get; set; } = [];
}

public class PublicProductForm
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(120)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@".*\S.*", ErrorMessage = "Urun adi zorunludur.")]
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

public class PublicProductViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    public int StockQuantity { get; set; }
    public decimal Weight { get; set; }
    public decimal PurityRate { get; set; }
    public decimal LaborCost { get; set; }
    public decimal LaborCostPercentage { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PublicProductEditForm : PublicProductForm
{
    public int Id { get; set; }
}

public class PublicProductFilterForm
{
    public string? Search { get; set; }
    public ProductType? Type { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public bool LowStockOnly { get; set; }
}

public class PublicDashboardViewModel
{
    public int TotalProducts { get; set; }
    public int TotalStock { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal NetProfit { get; set; }
    public int OpenOrders { get; set; }
    public int LowStockCount { get; set; }
}

public class PublicOrderSummaryViewModel
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PublicNotificationViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PublicReviewSummaryViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public ReviewStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
