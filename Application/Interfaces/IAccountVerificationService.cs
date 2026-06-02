using MGold.Application.DTOs;
using MGold.Domain.Entities;

namespace MGold.Application.Interfaces;

public interface IAccountVerificationService
{
    Task SendEmailConfirmationAsync(AppUser user, string baseUrl, string? requestIp, CancellationToken cancellationToken = default);
    Task SendEmailConfirmationAsync(string identifier, string baseUrl, string? requestIp, CancellationToken cancellationToken = default);
    Task<EmailVerificationResultDto> ConfirmEmailAsync(int userId, string token, CancellationToken cancellationToken = default);
    Task SendPhoneConfirmationAsync(string identifier, string? requestIp, CancellationToken cancellationToken = default);
    Task<EmailVerificationResultDto> ConfirmPhoneAsync(VerifyPhoneRequestDto dto, CancellationToken cancellationToken = default);
    Task StartPasswordResetAsync(ForgotPasswordRequestDto dto, string baseUrl, string? requestIp, CancellationToken cancellationToken = default);
    Task<EmailVerificationResultDto> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default);
}
