using MGold.Application.DTOs;
using MGold.Domain.Entities;

namespace MGold.Application.Mappings;

public static class MappingExtensions
{
    public static ProductDto ToDto(this Product entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Type = entity.Type,
            Weight = entity.Weight,
            PurityRate = entity.PurityRate,
            LaborCost = entity.LaborCost,
            LaborCostPercentage = entity.LaborCostPercentage,
            AdditionalCost = entity.AdditionalCost,
            ProfitMarginPercentage = entity.ProfitMarginPercentage,
            PurchasePrice = entity.PurchasePrice,
            SalePrice = entity.SalePrice,
            StockQuantity = entity.StockQuantity,
            AverageRating = entity.Reviews.Where(x => x.Status == Domain.Enums.ReviewStatus.Approved).Select(x => (decimal?)x.Rating).Average() ?? 0m,
            ReviewCount = entity.Reviews.Count(x => x.Status == Domain.Enums.ReviewStatus.Approved),
            IsLowStock = entity.StockQuantity <= 5
        };

    public static CustomerDto ToDto(this Customer entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Phone = entity.Phone,
            Email = entity.Email
        };

    public static TransactionDto ToDto(this Transaction entity)
        => new()
        {
            Id = entity.Id,
            ProductId = entity.ProductId,
            ProductName = entity.Product?.Name ?? string.Empty,
            CustomerId = entity.CustomerId,
            CustomerName = entity.Customer?.Name,
            Type = entity.Type,
            Quantity = entity.Quantity,
            UnitPrice = entity.UnitPrice,
            TotalPrice = entity.TotalPrice,
            GoldPricePerGramSnapshot = entity.GoldPricePerGramSnapshot,
            ProductWeightSnapshot = entity.ProductWeightSnapshot,
            PurityRateSnapshot = entity.PurityRateSnapshot,
            MaterialCostSnapshot = entity.MaterialCostSnapshot,
            LaborCostSnapshot = entity.LaborCostSnapshot,
            LaborCostPercentageSnapshot = entity.LaborCostPercentageSnapshot,
            AdditionalCostSnapshot = entity.AdditionalCostSnapshot,
            ProfitMarginPercentageSnapshot = entity.ProfitMarginPercentageSnapshot,
            CalculatedPurchasePriceSnapshot = entity.CalculatedPurchasePriceSnapshot,
            CalculatedSalePriceSnapshot = entity.CalculatedSalePriceSnapshot,
            TotalCostSnapshot = entity.TotalCostSnapshot,
            ProfitOrLoss = entity.ProfitOrLoss,
            Date = entity.Date
        };

    public static GoldPriceDto ToDto(this GoldPrice entity)
        => new()
        {
            Id = entity.Id,
            PricePerGram = entity.PricePerGram,
            EffectiveFrom = entity.EffectiveFrom,
            Source = entity.Source,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        };

    public static OrderDto ToDto(this Order entity)
        => new()
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            OrderNumber = entity.OrderNumber,
            CustomerId = entity.CustomerId,
            CustomerName = entity.Customer?.Name ?? string.Empty,
            CustomerPhone = entity.Customer?.Phone,
            CustomerEmail = entity.Customer?.Email,
            Status = entity.Status,
            Notes = entity.Notes,
            TotalAmount = entity.TotalAmount,
            PaidAmount = entity.PaidAmount,
            OutstandingAmount = Math.Max(entity.TotalAmount - entity.PaidAmount, 0m),
            PaymentStatus = entity.PaymentStatus,
            PreferredPaymentMethod = entity.PreferredPaymentMethod,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            DueDate = entity.DueDate,
            Items = entity.Items.Select(x => new OrderItemDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product?.Name ?? string.Empty,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                TotalPrice = x.TotalPrice,
                Notes = x.Notes
            }).ToList(),
            Payments = entity.Payments
                .OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
                .Select(x => new OrderPaymentDto
                {
                    Id = x.Id,
                    Method = x.Method,
                    Status = x.Status,
                    Amount = x.Amount,
                    ReferenceNumber = x.ReferenceNumber,
                    ProviderKey = x.ProviderKey,
                    ProviderTransactionId = x.ProviderTransactionId,
                    IdempotencyKey = x.IdempotencyKey,
                    InstallmentCount = x.InstallmentCount,
                    RequiresThreeDSecure = x.RequiresThreeDSecure,
                    ThreeDSecureStatus = x.ThreeDSecureStatus,
                    ParentPaymentId = x.ParentPaymentId,
                    IsRefund = x.IsRefund,
                    IsPartialRefund = x.IsPartialRefund,
                    FailureCode = x.FailureCode,
                    FailureMessage = x.FailureMessage,
                    Notes = x.Notes,
                    PaidAt = x.PaidAt,
                    CreatedAt = x.CreatedAt,
                    CreatedByUsername = x.CreatedByUsername
                }).ToList(),
            Invoices = entity.Invoices
                .OrderByDescending(x => x.InvoiceDate)
                .Select(x => new OrderInvoiceDto
                {
                    Id = x.Id,
                    InvoiceNumber = x.InvoiceNumber,
                    FileName = x.FileName,
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    InvoiceDate = x.InvoiceDate,
                    CreatedAt = x.CreatedAt
                }).ToList(),
            HistoryEntries = entity.HistoryEntries
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new OrderHistoryEntryDto
                {
                    Id = x.Id,
                    Type = x.Type,
                    Title = x.Title,
                    Description = x.Description,
                    ActorUsername = x.ActorUsername,
                    ActorRole = x.ActorRole,
                    RelatedEntityName = x.RelatedEntityName,
                    RelatedEntityId = x.RelatedEntityId,
                    MetadataJson = x.MetadataJson,
                    CreatedAt = x.CreatedAt
                }).ToList()
        };

    public static ProductReviewDto ToDto(this ProductReview entity)
        => new()
        {
            Id = entity.Id,
            ProductId = entity.ProductId,
            ProductName = entity.Product?.Name ?? string.Empty,
            CustomerId = entity.CustomerId,
            CustomerName = entity.Customer?.Name,
            Rating = entity.Rating,
            Comment = entity.Comment,
            Status = entity.Status,
            AdminReply = entity.AdminReply,
            CreatedAt = entity.CreatedAt,
            ModeratedAt = entity.ModeratedAt
        };

    public static NotificationDto ToDto(this Notification entity)
        => new()
        {
            Id = entity.Id,
            Title = entity.Title,
            Message = entity.Message,
            Type = entity.Type,
            TargetRole = entity.TargetRole,
            RelatedEntityName = entity.RelatedEntityName,
            RelatedEntityId = entity.RelatedEntityId,
            IsRead = entity.IsRead,
            IsCritical = entity.IsCritical,
            CreatedAt = entity.CreatedAt
        };

    public static ContactMessageDto ToDto(this ContactMessage entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Phone = entity.Phone,
            Email = entity.Email,
            Subject = entity.Subject,
            Message = entity.Message,
            IsResolved = entity.IsResolved,
            CreatedAt = entity.CreatedAt,
            ResolvedAt = entity.ResolvedAt,
            ResolutionNote = entity.ResolutionNote
        };
}
