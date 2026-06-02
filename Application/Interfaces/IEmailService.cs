using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IEmailService
{
    Task SendAsync(SendEmailRequestDto dto, CancellationToken cancellationToken = default);
}
