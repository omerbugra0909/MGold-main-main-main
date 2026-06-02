using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class CreateOrderDto
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime? DueDate { get; set; }
    public PaymentMethod? PreferredPaymentMethod { get; set; }

    [MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = [];
}

public class CreateOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? UnitPriceOverride { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus Status { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class CreateOrderPaymentDto
{
    [Required]
    public PaymentMethod Method { get; set; }

    [Required]
    public PaymentStatus Status { get; set; } = PaymentStatus.Paid;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(120)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(80)]
    public string? ProviderKey { get; set; }

    [MaxLength(160)]
    public string? ProviderTransactionId { get; set; }

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    [Range(1, 36)]
    public int InstallmentCount { get; set; } = 1;

    public bool RequiresThreeDSecure { get; set; }

    [MaxLength(40)]
    public string? ThreeDSecureStatus { get; set; }

    public int? ParentPaymentId { get; set; }

    public bool IsRefund { get; set; }

    [MaxLength(80)]
    public string? FailureCode { get; set; }

    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime? PaidAt { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public OrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod? PreferredPaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
    public IReadOnlyList<OrderPaymentDto> Payments { get; set; } = Array.Empty<OrderPaymentDto>();
    public IReadOnlyList<OrderInvoiceDto> Invoices { get; set; } = Array.Empty<OrderInvoiceDto>();
    public IReadOnlyList<OrderHistoryEntryDto> HistoryEntries { get; set; } = Array.Empty<OrderHistoryEntryDto>();
    public IReadOnlyList<RelatedNotificationDto> Notifications { get; set; } = Array.Empty<RelatedNotificationDto>();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Notes { get; set; }
}

public class OrderPaymentDto
{
    public int Id { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ProviderKey { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? IdempotencyKey { get; set; }
    public int InstallmentCount { get; set; }
    public bool RequiresThreeDSecure { get; set; }
    public string? ThreeDSecureStatus { get; set; }
    public int? ParentPaymentId { get; set; }
    public bool IsRefund { get; set; }
    public bool IsPartialRefund { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? Notes { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUsername { get; set; }
}

public class OrderInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderHistoryEntryDto
{
    public int Id { get; set; }
    public OrderHistoryType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ActorUsername { get; set; }
    public string? ActorRole { get; set; }
    public string? RelatedEntityName { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RelatedNotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsCritical { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
