using Microsoft.EntityFrameworkCore;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Infrastructure.Repositories;

public class GoldPriceRepository(AppDbContext context) : GenericRepository<GoldPrice>(context), IGoldPriceRepository
{
    public async Task<GoldPrice?> GetLatestActiveAsync(CancellationToken cancellationToken = default)
        => await Context.GoldPrices
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GoldPrice>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Context.GoldPrices
            .AsNoTracking()
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task DeactivateAllAsync(CancellationToken cancellationToken = default)
    {
        var activeRecords = await Context.GoldPrices.Where(x => x.IsActive).ToListAsync(cancellationToken);
        foreach (var record in activeRecords)
        {
            record.IsActive = false;
        }
    }
}
