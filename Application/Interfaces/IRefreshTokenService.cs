using MGold.Application.DTOs;
using MGold.Domain.Entities;

namespace MGold.Application.Interfaces;

public interface IRefreshTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> CreateAsync(AppUser user, string? deviceName, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshAsync(string refreshToken, string? deviceName, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(int userId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthSessionDto>> GetSessionsForCurrentUserAsync(CancellationToken cancellationToken = default);
}
