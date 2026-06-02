using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IPaymentProvider
{
    string ProviderKey { get; }
    Task<PaymentProviderResultDto> AuthorizeOrCaptureAsync(PaymentProviderRequestDto request, CancellationToken cancellationToken = default);
    Task<PaymentProviderResultDto> RefundAsync(PaymentRefundRequestDto request, CancellationToken cancellationToken = default);
}
