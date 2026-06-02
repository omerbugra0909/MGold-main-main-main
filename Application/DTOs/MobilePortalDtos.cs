using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class CustomerPortalDashboardDto
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
    public IReadOnlyList<CustomerOrderSummaryDto> Orders { get; set; } = [];
    public IReadOnlyList<CustomerFavoriteProductDto> FavoriteHighlights { get; set; } = [];
    public IReadOnlyList<MarketQuoteDto> MarketHighlights { get; set; } = [];
    public IReadOnlyList<CustomerContactMessageDto> RecentContactMessages { get; set; } = [];
}

public class CustomerProductListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public decimal Weight { get; set; }
    public bool IsFavorite { get; set; }
}

public class CustomerFavoriteProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    public decimal SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerOrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<string> Items { get; set; } = [];
}

public class CustomerContactMessageDto
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
}

public class ToggleFavoriteResultDto
{
    public int ProductId { get; set; }
    public bool IsFavorite { get; set; }
}

public class ToggleMarketWatchlistResultDto
{
    public string Symbol { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
