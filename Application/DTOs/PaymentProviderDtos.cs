using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class PaymentProviderRequestDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public PaymentMethod Method { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public bool UseThreeDSecure { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? CardToken { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
}

public class PaymentProviderResultDto
{
    public bool Success { get; set; }
    public bool RequiresRedirect { get; set; }
    public string? RedirectUrl { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string? ReferenceNumber { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? ThreeDSecureStatus { get; set; }
}

public class PaymentRefundRequestDto
{
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string? Reason { get; set; }
}
