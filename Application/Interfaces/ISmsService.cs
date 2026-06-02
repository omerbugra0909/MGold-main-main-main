using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface ISmsService
{
    Task SendAsync(SendSmsRequestDto dto, CancellationToken cancellationToken = default);
}
