using Microsoft.EntityFrameworkCore;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Infrastructure.Repositories;

public class ProductRepository(AppDbContext context) : GenericRepository<Product>(context), IProductRepository
{
    public async Task<bool> HasTransactionsAsync(int productId, CancellationToken cancellationToken = default)
        => await Context.Transactions.AnyAsync(x => x.ProductId == productId, cancellationToken);
}
