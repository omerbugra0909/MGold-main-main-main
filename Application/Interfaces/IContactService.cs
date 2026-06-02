using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IContactService
{
    Task<ContactMessageDto> SubmitAsync(SubmitContactMessageDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactMessageDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ContactMessageDto> ResolveAsync(int id, ResolveContactMessageDto dto, CancellationToken cancellationToken = default);
}
