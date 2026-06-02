using MGold.Domain.Entities;

namespace MGold.Infrastructure.Repositories.Interfaces;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<IReadOnlyList<Transaction>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ProductHasOtherTransactionsAsync(int productId, int excludedTransactionId, CancellationToken cancellationToken = default);
}
