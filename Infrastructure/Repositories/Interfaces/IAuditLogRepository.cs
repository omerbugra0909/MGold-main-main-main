using MGold.Domain.Entities;

namespace MGold.Infrastructure.Repositories.Interfaces;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
