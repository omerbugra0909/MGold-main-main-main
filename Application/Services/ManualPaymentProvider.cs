using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Domain.Enums;

namespace MGold.Application.Services;

public class ManualPaymentProvider : IPaymentProvider
{
    public const string Key = "manual";
    public string ProviderKey => Key;

    public Task<PaymentProviderResultDto> AuthorizeOrCaptureAsync(PaymentProviderRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Payment amount must be greater than zero.");
        }

        return Task.FromResult(new PaymentProviderResultDto
        {
            Success = true,
            ProviderKey = ProviderKey,
            ReferenceNumber = request.IdempotencyKey,
            Status = PaymentStatus.Paid,
            ThreeDSecureStatus = request.UseThreeDSecure ? "not_applicable_manual" : null
        });
    }

    public Task<PaymentProviderResultDto> RefundAsync(PaymentRefundRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Refund amount must be greater than zero.");
        }

        return Task.FromResult(new PaymentProviderResultDto
        {
            Success = true,
            ProviderKey = ProviderKey,
            ReferenceNumber = request.IdempotencyKey,
            Status = PaymentStatus.Refunded
        });
    }
}
