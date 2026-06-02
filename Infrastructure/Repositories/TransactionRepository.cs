using Microsoft.EntityFrameworkCore;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Infrastructure.Repositories;

public class TransactionRepository(AppDbContext context) : GenericRepository<Transaction>(context), ITransactionRepository
{
    public async Task<IReadOnlyList<Transaction>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        => await Context.Transactions
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

    public async Task<Transaction?> GetByIdWithDetailsAsync(int id, CancellationToken cancellationToken = default)
        => await Context.Transactions
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ProductHasOtherTransactionsAsync(int productId, int excludedTransactionId, CancellationToken cancellationToken = default)
        => await Context.Transactions.AnyAsync(
            x => x.ProductId == productId && x.Id != excludedTransactionId,
            cancellationToken);
}
