using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Common;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Controllers;

[ApiController]
[Authorize(Roles = RoleConstants.CustomerOnly)]
[Produces("application/json")]
[Consumes("application/json")]
[Route("api/customer")]
public class CustomerPortalController(
    AppDbContext context,
    IContactService contactService,
    IMarketDataService marketDataService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await BuildDashboardAsync(cancellationToken), "Customer dashboard retrieved successfully.");

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        return ApiResponseFactory.Create(this, new
        {
            user.Id,
            user.CustomerId,
            user.CompanyId,
            user.Username,
            user.FullName,
            user.Email,
            user.Phone,
            user.ThemePreference,
            user.EmailConfirmed,
            user.PhoneConfirmed
        }, "Customer profile retrieved successfully.");
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string? search, [FromQuery] ProductType? type, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var query = context.Products.AsNoTracking().AsQueryable();
        if (user.CompanyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == user.CompanyId);
        }

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

        var products = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Name)
            .Select(x => new CustomerProductListItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Type = x.Type,
                SalePrice = x.SalePrice,
                StockQuantity = x.StockQuantity,
                Weight = x.Weight,
                IsFavorite = favorites.Contains(x.Id)
            })
            .ToListAsync(cancellationToken);

        return ApiResponseFactory.Create(this, products, "Customer products retrieved successfully.");
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var favorites = await BuildFavoritesQuery(user)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CustomerFavoriteProductDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Type = x.Product.Type,
                SalePrice = x.Product.SalePrice,
                StockQuantity = x.Product.StockQuantity,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponseFactory.Create(this, favorites, "Favorites retrieved successfully.");
    }

    [HttpPost("favorites/{productId:int}/toggle")]
    public async Task<IActionResult> ToggleFavorite(int productId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var product = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with id {productId} was not found.");

        if (user.CompanyId.HasValue && product.CompanyId != user.CompanyId)
        {
            return ApiResponseFactory.CreateFailure(this, "Forbidden.", StatusCodes.Status403Forbidden, "Bu ürüne erişim yetkiniz yok.");
        }

        var favorite = await context.CustomerFavorites
            .FirstOrDefaultAsync(x => x.CustomerId == user.CustomerId && x.ProductId == productId, cancellationToken);

        var isFavorite = favorite is null;
        if (favorite is null)
        {
            await context.CustomerFavorites.AddAsync(new CustomerFavorite
            {
                CustomerId = user.CustomerId!.Value,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            context.CustomerFavorites.Remove(favorite);
        }

        await context.SaveChangesAsync(cancellationToken);
        return ApiResponseFactory.Create(this, new ToggleFavoriteResultDto { ProductId = productId, IsFavorite = isFavorite }, "Favorite state updated successfully.");
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
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

        return ApiResponseFactory.Create(this, orders.Select(ToCustomerOrderSummary).ToList(), "Customer orders retrieved successfully.");
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> GetOrderDetails(int id, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var order = await context.Orders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Payments)
            .Include(x => x.Invoices)
            .Include(x => x.HistoryEntries)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == user.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order with id {id} was not found.");

        var dto = order.ToDto();
        dto.Notifications = await context.Notifications
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

        return ApiResponseFactory.Create(this, dto, "Customer order retrieved successfully.");
    }

    [HttpGet("contact/messages")]
    public async Task<IActionResult> GetContactMessages(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        var messages = await context.ContactMessages
            .AsNoTracking()
            .Where(x => x.Email == user.Email)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CustomerContactMessageDto
            {
                Subject = x.Subject,
                Message = x.Message,
                CreatedAt = x.CreatedAt,
                IsResolved = x.IsResolved
            })
            .ToListAsync(cancellationToken);

        return ApiResponseFactory.Create(this, messages, "Customer contact messages retrieved successfully.");
    }

    [HttpPost("contact/messages")]
    public async Task<IActionResult> SubmitContactMessage([FromBody] SubmitContactMessageDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await contactService.SubmitAsync(dto, cancellationToken), "Contact message submitted successfully.", StatusCodes.Status201Created);

    private async Task<CustomerPortalDashboardDto> BuildDashboardAsync(CancellationToken cancellationToken)
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

        var favorites = await BuildFavoritesQuery(user)
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

        return new CustomerPortalDashboardDto
        {
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            OrderCount = orders.Count,
            OpenOrderCount = orders.Count(x => x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled),
            TotalSpent = orders.Sum(x => x.PaidAmount),
            FavoriteCount = await BuildFavoritesQuery(user).CountAsync(cancellationToken),
            MarketWatchlistCount = market.Watchlist.Count,
            ProductsCount = await context.Products.AsNoTracking().CountAsync(x => !user.CompanyId.HasValue || x.CompanyId == user.CompanyId, cancellationToken),
            UnresolvedContactCount = recentMessages.Count(x => !x.IsResolved),
            Orders = orders.Select(ToCustomerOrderSummary).ToList(),
            FavoriteHighlights = favorites.Select(x => new CustomerFavoriteProductDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Type = x.Product.Type,
                SalePrice = x.Product.SalePrice,
                StockQuantity = x.Product.StockQuantity,
                CreatedAt = x.CreatedAt
            }).ToList(),
            MarketHighlights = market.TopMovers.Take(3).ToList(),
            RecentContactMessages = recentMessages.Select(x => new CustomerContactMessageDto
            {
                Subject = x.Subject,
                Message = x.Message,
                CreatedAt = x.CreatedAt,
                IsResolved = x.IsResolved
            }).ToList()
        };
    }

    private IQueryable<CustomerFavorite> BuildFavoritesQuery(AppUser user)
        => context.CustomerFavorites
            .AsNoTracking()
            .Where(x => x.CustomerId == user.CustomerId)
            .Include(x => x.Product)
            .Where(x => !user.CompanyId.HasValue || x.Product.CompanyId == user.CompanyId);

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

    private static CustomerOrderSummaryDto ToCustomerOrderSummary(Order order)
        => new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            TotalAmount = order.TotalAmount,
            PaidAmount = order.PaidAmount,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => $"{i.Product?.Name ?? $"Ürün #{i.ProductId}"} x{i.Quantity}").ToList()
        };
}
