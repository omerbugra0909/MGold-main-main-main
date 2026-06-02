using MGold.Domain.Entities;

namespace MGold.Infrastructure.Repositories.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<bool> HasTransactionsAsync(int productId, CancellationToken cancellationToken = default);
}
