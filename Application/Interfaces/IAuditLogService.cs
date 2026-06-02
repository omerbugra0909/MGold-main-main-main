using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IAuditLogService
{
    Task WriteAsync(CreateAuditLogDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);
}
