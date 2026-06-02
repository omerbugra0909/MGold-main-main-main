using Microsoft.EntityFrameworkCore;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Infrastructure.Repositories;

public class AppUserRepository(AppDbContext context) : GenericRepository<AppUser>(context), IAppUserRepository
{
    public async Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await Context.AppUsers
            .Include(x => x.Company)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Context.AppUsers
            .Include(x => x.Company)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public async Task<AppUser?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
        => await Context.AppUsers
            .Include(x => x.Company)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Phone == phone, cancellationToken);

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => await Context.AppUsers.AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<AppUser>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Context.AppUsers
            .AsNoTracking()
            .Include(x => x.Company)
            .Include(x => x.Customer)
            .OrderBy(x => x.Username)
            .ToListAsync(cancellationToken);
}
