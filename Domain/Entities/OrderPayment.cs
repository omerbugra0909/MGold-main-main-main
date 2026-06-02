using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Domain.Entities;

public class OrderPayment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }

    [MaxLength(120)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(80)]
    public string? ProviderKey { get; set; }

    [MaxLength(160)]
    public string? ProviderTransactionId { get; set; }

    [MaxLength(120)]
    public string? IdempotencyKey { get; set; }

    public int InstallmentCount { get; set; } = 1;

    public bool RequiresThreeDSecure { get; set; }

    [MaxLength(40)]
    public string? ThreeDSecureStatus { get; set; }

    public int? ParentPaymentId { get; set; }
    public OrderPayment? ParentPayment { get; set; }

    public bool IsRefund { get; set; }
    public bool IsPartialRefund { get; set; }

    [MaxLength(80)]
    public string? FailureCode { get; set; }

    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    public string? CreatedByUsername { get; set; }
}
