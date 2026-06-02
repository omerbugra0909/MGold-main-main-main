using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Infrastructure.Repositories;

public class CustomerRepository(AppDbContext context) : GenericRepository<Customer>(context), ICustomerRepository
{
}
