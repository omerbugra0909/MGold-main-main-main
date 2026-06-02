using MGold.Domain.Entities;

namespace MGold.Infrastructure.Repositories.Interfaces;

public interface IGoldPriceRepository : IGenericRepository<GoldPrice>
{
    Task<GoldPrice?> GetLatestActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoldPrice>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    Task DeactivateAllAsync(CancellationToken cancellationToken = default);
}
