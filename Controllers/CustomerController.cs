using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Controllers;

[Authorize(Roles = RoleConstants.CustomerOnly)]
[Route("customer")]
public class CustomerController(
    AppDbContext context,
    IInvoiceService invoiceService,
    IContactService contactService,
    IMarketDataService marketDataService) : Controller
{
    [HttpGet("")]
    [HttpGet("/home")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await BuildDashboardAsync(cancellationToken));

    [HttpGet("products")]
    public async Task<IActionResult> Products(int? companyId, string? search, ProductType? type, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (!companyId.HasValue)
        {
            return RedirectToAction("Companies", "Public");
        }

        var company = await context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == companyId.Value && x.IsActive, cancellationToken);
        if (company is null)
        {
            TempData["Error"] = "Firma bulunamadı veya yayında değil.";
            return RedirectToAction("Companies", "Public");
        }

        var query = context.Products.AsNoTracking().Where(x => x.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(normalized));
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        var favorites = await context.CustomerFavorites
            .AsNoTracking()
            .Where(x => x.CustomerId == user.CustomerId)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        var model = new CustomerProductsPageViewModel
        {
            Search = search,
            Type = type,
            CompanyId = company.Id,
            CompanyName = company.Name,
            Products = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Name)
                .Select(x => new CustomerProductCardViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Type = x.Type,
                    SalePrice = x.SalePrice,
                    StockQuantity = x.StockQuantity,
                    Weight = x.Weight,
                    IsFavorite = favorites.Contains(x.Id)
                })
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost("favorites/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int productId, string? returnUrl, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var product = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
        {
            TempData["Error"] = "Ürün bulunamadı veya bu ürüne erişim yetkiniz yok.";
            return RedirectToLocal(returnUrl, nameof(Favorites));
        }

        var favorite = await context.CustomerFavorites
            .FirstOrDefaultAsync(x => x.CustomerId == user.CustomerId && x.ProductId == productId, cancellationToken);

        if (favorite is null)
        {
            await context.CustomerFavorites.AddAsync(new CustomerFavorite
            {
                CustomerId = user.CustomerId!.Value,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            TempData["Success"] = "Ürün favorilere eklendi.";
        }
        else
        {
            context.CustomerFavorites.Remove(favorite);
            TempData["Success"] = "Ürün favorilerden çıkarıldı.";
        }

        await context.SaveChangesAsync(cancellationToken);
        return RedirectToLocal(returnUrl, nameof(Favorites));
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var model = new CustomerFavoritesPageViewModel
        {
            Favorites = await context.CustomerFavorites
                .AsNoTracking()
                .Where(x => x.CustomerId == user.CustomerId)
                .Include(x => x.Product)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomerFavoriteViewModel
                {
                    ProductId = x.ProductId,
                    ProductName = x.Product.Name,
                    Type = x.Product.Type,
                    SalePrice = x.Product.SalePrice,
                    StockQuantity = x.Product.StockQuantity,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken cancellationToken)
    {
        var model = await BuildDashboardAsync(cancellationToken);
        return View(model);
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> OrderDetails(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var order = await context.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Payments)
            .Include(x => x.Invoices)
            .Include(x => x.HistoryEntries)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == user.CustomerId, cancellationToken);

        if (order is null)
        {
            return RedirectToAction(nameof(Orders));
        }

        var notifications = await context.Notifications
            .AsNoTracking()
            .Where(x => x.RelatedEntityName == nameof(Order) && x.RelatedEntityId == order.Id.ToString())
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RelatedNotificationDto
            {
                Id = x.Id,
                Title = x.Title,
                Message = x.Message,
                Type = x.Type,
                IsCritical = x.IsCritical,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var model = order.ToDto();
        model.Notifications = notifications;
        return View(model);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
        => View(await BuildDashboardAsync(cancellationToken));

    [HttpGet("contact")]
    public async Task<IActionResult> Contact(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var model = new CustomerContactPageViewModel
        {
            Form = new CustomerContactForm
            {
                Name = user.FullName,
                Email = user.Email,
                Phone = user.Phone
            },
            RecentMessages = await context.ContactMessages
                .AsNoTracking()
                .Where(x => x.Email == user.Email)
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .Select(x => new CustomerContactMessageSummary
                {
                    Subject = x.Subject,
                    Message = x.Message,
                    CreatedAt = x.CreatedAt,
                    IsResolved = x.IsResolved
                })
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(CustomerContactForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = await Contact(cancellationToken) as ViewResult;
            if (fallback?.Model is CustomerContactPageViewModel model)
            {
                model.Form = form;
                return View(model);
            }

            return View(new CustomerContactPageViewModel { Form = form });
        }

        await contactService.SubmitAsync(new SubmitContactMessageDto
        {
            Name = form.Name,
            Email = form.Email,
            Phone = form.Phone,
            Subject = form.Subject,
            Message = form.Message
        }, cancellationToken);

        TempData["Success"] = "Mesajiniz destek ekibine iletildi.";
        return RedirectToAction(nameof(Contact));
    }

    [HttpGet("invoices/{invoiceId:int}/download")]
    public async Task<IActionResult> DownloadInvoice(int invoiceId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var invoice = await context.OrderInvoices
            .AsNoTracking()
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && x.Order.CustomerId == user.CustomerId, cancellationToken);

        if (invoice is null)
        {
            return RedirectToAction(nameof(Orders));
        }

        var pdf = await invoiceService.GetPdfAsync(invoiceId, cancellationToken);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    private async Task<CustomerPanelViewModel> BuildDashboardAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var orders = await context.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == user.CustomerId)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Payments)
            .Include(x => x.Invoices)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var favorites = await context.CustomerFavorites
            .AsNoTracking()
            .Where(x => x.CustomerId == user.CustomerId)
            .Include(x => x.Product)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .ToListAsync(cancellationToken);

        var recentMessages = await context.ContactMessages
            .AsNoTracking()
            .Where(x => x.Email == user.Email)
            .OrderByDescending(x => x.CreatedAt)
            .Take(3)
            .ToListAsync(cancellationToken);
        var market = await marketDataService.GetDashboardAsync("TRY", user.Username, cancellationToken);

        return new CustomerPanelViewModel
        {
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            OrderCount = orders.Count,
            OpenOrderCount = orders.Count(x => x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled),
            TotalSpent = orders.Sum(x => x.PaidAmount),
            FavoriteCount = favorites.Count,
            MarketWatchlistCount = market.Watchlist.Count,
            ProductsCount = await context.Products
                .AsNoTracking()
                .CountAsync(x => x.CompanyId.HasValue && x.Company != null && x.Company.IsActive, cancellationToken),
            UnresolvedContactCount = recentMessages.Count(x => !x.IsResolved),
            Orders = orders.Select(x => new CustomerOrderSummaryViewModel
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                Status = x.Status.ToString(),
                PaymentStatus = x.PaymentStatus.ToString(),
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                CreatedAt = x.CreatedAt,
                Items = x.Items.Select(i => $"{i.Product.Name} x{i.Quantity}").ToList()
            }).ToList(),
            FavoriteHighlights = favorites.Select(x => new CustomerFavoriteViewModel
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Type = x.Product.Type,
                SalePrice = x.Product.SalePrice,
                StockQuantity = x.Product.StockQuantity,
                CreatedAt = x.CreatedAt
            }).ToList(),
            MarketHighlights = market.TopMovers.Take(3).Select(x => new CustomerMarketHighlightViewModel
            {
                Symbol = x.Symbol,
                DisplayName = x.DisplayName,
                Price = x.Price,
                Change24hPercent = x.Change24hPercent,
                DisplayCurrency = x.DisplayCurrency
            }).ToList(),
            RecentContactMessages = recentMessages.Select(x => new CustomerContactMessageSummary
            {
                Subject = x.Subject,
                Message = x.Message,
                CreatedAt = x.CreatedAt,
                IsResolved = x.IsResolved
            }).ToList()
        };
    }

    private async Task<AppUser> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(rawUserId, out var userId))
        {
            throw new InvalidOperationException("Authenticated customer could not be resolved.");
        }

        var user = await context.AppUsers
            .AsNoTracking()
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Authenticated customer could not be resolved.");

        if (user.CustomerId is null)
        {
            throw new InvalidOperationException("Authenticated customer account is not linked to a customer profile.");
        }

        return user;
    }

    private IActionResult RedirectToLocal(string? returnUrl, string fallbackAction)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(fallbackAction);
    }
}

public class CustomerPanelViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int OpenOrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public int FavoriteCount { get; set; }
    public int MarketWatchlistCount { get; set; }
    public int ProductsCount { get; set; }
    public int UnresolvedContactCount { get; set; }
    public IReadOnlyList<CustomerOrderSummaryViewModel> Orders { get; set; } = [];
    public IReadOnlyList<CustomerFavoriteViewModel> FavoriteHighlights { get; set; } = [];
    public IReadOnlyList<CustomerMarketHighlightViewModel> MarketHighlights { get; set; } = [];
    public IReadOnlyList<CustomerContactMessageSummary> RecentContactMessages { get; set; } = [];
}

public class CustomerMarketHighlightViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Change24hPercent { get; set; }
    public string DisplayCurrency { get; set; } = "TRY";
}

public class CustomerOrderSummaryViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<string> Items { get; set; } = [];
}

public class CustomerProductsPageViewModel
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Search { get; set; }
    public ProductType? Type { get; set; }
    public IReadOnlyList<CustomerProductCardViewModel> Products { get; set; } = [];
}

public class CustomerProductCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public decimal Weight { get; set; }
    public bool IsFavorite { get; set; }
}

public class CustomerFavoritesPageViewModel
{
    public IReadOnlyList<CustomerFavoriteViewModel> Favorites { get; set; } = [];
}

public class CustomerFavoriteViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerContactPageViewModel
{
    public CustomerContactForm Form { get; set; } = new();
    public IReadOnlyList<CustomerContactMessageSummary> RecentMessages { get; set; } = [];
}

public class CustomerContactForm
{
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    [RegularExpression(@"^(\+90|90|0)?5\d{9}$")]
    public string Phone { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string Subject { get; set; } = string.Empty;
    [Required]
    public string Message { get; set; } = string.Empty;
}

public class CustomerContactMessageSummary
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
}
