using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class RefreshTokenService(
    AppDbContext context,
    IJwtTokenService jwtTokenService,
    ICurrentUserService currentUserService,
    IOptions<JwtSettings> settings) : IRefreshTokenService
{
    public async Task<(string Token, DateTime ExpiresAtUtc)> CreateAsync(
        AppUser user,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateToken();
        var entity = new RefreshToken
        {
            AppUserId = user.Id,
            TokenHash = Hash(rawToken),
            SecurityStampSnapshot = user.SecurityStamp,
            DeviceName = Normalize(deviceName, 160),
            CreatedByIp = Normalize(ipAddress, 80),
            CreatedByUserAgent = Normalize(userAgent, 300),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(Math.Clamp(settings.Value.RefreshTokenExpiryDays, 1, 180))
        };

        context.RefreshTokens.Add(entity);
        await PruneOldTokensAsync(user.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return (rawToken, entity.ExpiresAt);
    }

    public async Task<AuthResponseDto> RefreshAsync(
        string refreshToken,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(refreshToken);
        var existing = await context.RefreshTokens
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (existing.IsRevoked)
        {
            await RevokeAllForUserAsync(existing.AppUserId, ipAddress, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token reuse detected. All sessions were revoked.");
        }

        if (existing.IsExpired)
        {
            throw new UnauthorizedAccessException("Refresh token expired.");
        }

        var user = existing.AppUser;
        if (!user.IsActive || !user.EmailConfirmed || existing.SecurityStampSnapshot != user.SecurityStamp)
        {
            await RevokeAllForUserAsync(user.Id, ipAddress, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token is no longer valid.");
        }

        var newRawToken = GenerateToken();
        var newHash = Hash(newRawToken);
        var now = DateTime.UtcNow;
        existing.RevokedAt = now;
        existing.RevokedByIp = Normalize(ipAddress, 80);
        existing.ReplacedByTokenHash = newHash;

        var newRefreshToken = new RefreshToken
        {
            AppUserId = user.Id,
            TokenHash = newHash,
            SecurityStampSnapshot = user.SecurityStamp,
            DeviceName = Normalize(deviceName ?? existing.DeviceName, 160),
            CreatedByIp = Normalize(ipAddress, 80),
            CreatedByUserAgent = Normalize(userAgent, 300),
            CreatedAt = now,
            ExpiresAt = now.AddDays(Math.Clamp(settings.Value.RefreshTokenExpiryDays, 1, 180))
        };
        context.RefreshTokens.Add(newRefreshToken);
        await PruneOldTokensAsync(user.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateToken(user);
        return BuildResponse(user, accessToken.Token, accessToken.ExpiresAtUtc, newRawToken, newRefreshToken.ExpiresAt);
    }

    public async Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(refreshToken);
        var existing = await context.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (existing is null || existing.IsRevoked)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = Normalize(ipAddress, 80);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(int userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await context.RefreshTokens
            .Where(x => x.AppUserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = Normalize(ipAddress, 80);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthSessionDto>> GetSessionsForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new UnauthorizedAccessException("Authentication is required.");
        return await context.RefreshTokens
            .AsNoTracking()
            .Where(x => x.AppUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .Select(x => new AuthSessionDto
            {
                Id = x.Id,
                DeviceName = x.DeviceName,
                CreatedAt = x.CreatedAt,
                ExpiresAt = x.ExpiresAt,
                RevokedAt = x.RevokedAt,
                IsActive = x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow
            })
            .ToListAsync(cancellationToken);
    }

    private async Task PruneOldTokensAsync(int userId, CancellationToken cancellationToken)
    {
        var maxTokens = Math.Clamp(settings.Value.MaxRefreshTokensPerUser, 1, 50);
        var activeTokens = await context.RefreshTokens
            .Where(x => x.AppUserId == userId && x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(maxTokens - 1)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIp = "system-prune";
        }
    }

    private static AuthResponseDto BuildResponse(AppUser user, string accessToken, DateTime accessExpiresAtUtc, string refreshToken, DateTime refreshExpiresAtUtc)
        => new()
        {
            UserId = user.Id,
            CompanyId = user.CompanyId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = ToClientRole(user.Role),
            SystemRole = user.Role,
            RedirectUrl = GetHomePath(user.Role),
            EmailConfirmed = user.EmailConfirmed,
            ThemePreference = string.Equals(user.ThemePreference, "diamond-silver", StringComparison.OrdinalIgnoreCase) ? "diamond-silver" : "gold-premium",
            Token = accessToken,
            ExpiresAtUtc = accessExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpiresAtUtc
        };

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    private static string? Normalize(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string ToClientRole(string systemRole)
        => systemRole switch
        {
            MGold.Domain.Constants.RoleConstants.SystemAdmin => "admin",
            MGold.Domain.Constants.RoleConstants.Manager => "shopOwner",
            MGold.Domain.Constants.RoleConstants.Employee => "employee",
            MGold.Domain.Constants.RoleConstants.Customer => "customer",
            _ => "guest"
        };

    private static string GetHomePath(string role)
        => role switch
        {
            MGold.Domain.Constants.RoleConstants.SystemAdmin => "/admin",
            MGold.Domain.Constants.RoleConstants.Manager => "/owner",
            MGold.Domain.Constants.RoleConstants.Employee => "/employee",
            MGold.Domain.Constants.RoleConstants.Customer => "/home",
            _ => "/auth"
        };
}
