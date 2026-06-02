using Microsoft.EntityFrameworkCore;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Infrastructure.Repositories;

public class AuditLogRepository(AppDbContext context) : GenericRepository<AuditLog>(context), IAuditLogRepository
{
    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        => await Context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
}
