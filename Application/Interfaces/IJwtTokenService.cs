using MGold.Domain.Entities;

namespace MGold.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(AppUser user);
}
