using MGold.Domain.Entities;

namespace MGold.Infrastructure.Repositories.Interfaces;

public interface IAppUserRepository : IGenericRepository<AppUser>
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
}
