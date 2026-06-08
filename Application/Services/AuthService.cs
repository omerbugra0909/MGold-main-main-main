using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Entities;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Application.Services;

public class AuthService(
    IAppUserRepository appUserRepository,
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService,
    IAuthRouteService authRouteService,
    IAccessControlService accessControlService,
    IPasswordHasher<AppUser> passwordHasher,
    IRefreshTokenService refreshTokenService,
    IHttpContextAccessor httpContextAccessor) : IAuthService
{
    public async Task<AuthBootstrapStatusDto> GetBootstrapStatusAsync(CancellationToken cancellationToken = default)
    {
        var hasUsers = await appUserRepository.AnyAsync(cancellationToken);
        return new AuthBootstrapStatusDto
        {
            HasUsers = hasUsers,
            RequiresSetup = !hasUsers
        };
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = dto.Username.Trim().ToLowerInvariant();
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var normalizedPhone = TurkishPhoneHelper.Normalize(dto.Phone);
        ValidatePasswordStrength(dto.Password);

        if (await appUserRepository.GetByUsernameAsync(normalizedUsername, cancellationToken) is not null)
        {
            throw new BusinessRuleException("A user with this username already exists.");
        }

        if (await appUserRepository.GetByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            throw new BusinessRuleException("A user with this email already exists.");
        }

        if (await appUserRepository.GetByPhoneAsync(normalizedPhone, cancellationToken) is not null)
        {
            throw new BusinessRuleException("A user with this phone already exists.");
        }

        var requestedRole = Domain.Constants.RoleConstants.Customer;

        var customer = new Customer
        {
            Name = dto.FullName.Trim(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            CompanyId = null
        };

        var user = new AppUser
        {
            Username = normalizedUsername,
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            Phone = normalizedPhone,
            Role = requestedRole,
            CompanyId = null,
            Customer = customer,
            IsActive = false,
            EmailConfirmed = false,
            PhoneConfirmed = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        await appUserRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
        => await LoginInternalAsync(dto, dto.RequireAdminPortal, cancellationToken, restrictToCustomerPortal: false);

    public async Task<AuthResponseDto> LoginCustomerAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
        => await LoginInternalAsync(dto, requireAdminPortal: false, cancellationToken, restrictToCustomerPortal: true);

    public async Task<AuthResponseDto> LoginAdminAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
        => await LoginInternalAsync(dto, requireAdminPortal: true, cancellationToken);

    public async Task<AuthResponseDto> LoginWorkspaceAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
        => await LoginInternalAsync(dto, requireAdminPortal: false, cancellationToken, requireWorkspacePortal: true);

    private async Task<AuthResponseDto> LoginInternalAsync(
        LoginRequestDto dto,
        bool requireAdminPortal,
        CancellationToken cancellationToken,
        bool requireWorkspacePortal = false,
        bool restrictToCustomerPortal = false)
    {
        var lookup = dto.EmailOrUsername.Trim().ToLowerInvariant();
        AppUser? user = null;

        var lookupAliases = GetLoginLookupAliases(lookup);

        foreach (var candidate in lookupAliases)
        {
            if (candidate.Contains('@'))
            {
                user = await appUserRepository.GetByEmailAsync(candidate, cancellationToken)
                    ?? await appUserRepository.GetByUsernameAsync(candidate, cancellationToken);
            }
            else
            {
                user = await appUserRepository.GetByUsernameAsync(candidate, cancellationToken)
                    ?? await appUserRepository.GetByEmailAsync(candidate, cancellationToken);
            }

            if (user is not null)
            {
                break;
            }
        }

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException($"Çok fazla hatalı deneme yapildi. Lutfen {user.LockoutEndAt.Value.ToLocalTime():HH:mm} sonrasinda tekrar deneyin.");
        }

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedAccessException("E-posta adresiniz dogrulanmamis. Lutfen gelen kutunuzu kontrol edin veya doğrulama mailini yeniden isteyin.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account is inactive.");
        }

        if (requireAdminPortal
            && !string.Equals(user.Role, Domain.Constants.RoleConstants.SystemAdmin, StringComparison.Ordinal)
            && !string.Equals(user.Role, Domain.Constants.RoleConstants.Manager, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Bu giriş ekrani sadece sistem admini ve firma yöneticisi hesapları içindir.");
        }

        if (requireWorkspacePortal
            && !string.Equals(user.Role, Domain.Constants.RoleConstants.Manager, StringComparison.Ordinal)
            && !string.Equals(user.Role, Domain.Constants.RoleConstants.Employee, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Bu giriş alanı yalnızca ic ekip hesapları içindir.");
        }

        if (user.Role is Domain.Constants.RoleConstants.Manager or Domain.Constants.RoleConstants.Employee)
        {
            if (!user.CompanyId.HasValue)
            {
                throw new UnauthorizedAccessException("Bu ic ekip hesabına bağlı firma bulunamadı.");
            }

            if (user.Company is not null && !user.Company.IsActive)
            {
                throw new UnauthorizedAccessException("Bu firmaya ait hesaplar geçici olarak pasiftir.");
            }
        }

        if (restrictToCustomerPortal
            && !string.Equals(user.Role, Domain.Constants.RoleConstants.Customer, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Müşteri giriş alanı yalnızca müşteri hesapları içindir.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.LockoutEndAt = DateTime.UtcNow.AddMinutes(15);
                user.AccessFailedCount = 0;
            }

            appUserRepository.Update(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.AccessFailedCount = 0;
        user.LockoutEndAt = null;
        appUserRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = BuildAuthResponse(user);
        var httpContext = httpContextAccessor.HttpContext;
        var refreshToken = await refreshTokenService.CreateAsync(
            user,
            dto.DeviceName,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString(),
            cancellationToken);
        response.RefreshToken = refreshToken.Token;
        response.RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc;
        return response;
    }

    private static IReadOnlyList<string> GetLoginLookupAliases(string lookup)
    {
        if (string.Equals(lookup, "sakizciomerbugra895gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { lookup, "sakizciomerbugra895@gmail.com" };
        }

        return new[] { lookup };
    }

    public async Task<IReadOnlyList<UserInfoDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureAdminOnly();

        return (await appUserRepository.GetAllOrderedAsync(cancellationToken))
            .Select(x => new UserInfoDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                CompanyName = x.Company?.Name,
                Username = x.Username,
                FullName = x.FullName,
                Email = x.Email,
                Phone = x.Phone,
                Role = x.Role,
                IsActive = x.IsActive,
                CustomerId = x.CustomerId,
                ThemePreference = NormalizeThemePreference(x.ThemePreference),
                CreatedAt = x.CreatedAt,
                LastLoginAt = x.LastLoginAt
            })
            .ToList();
    }

    private AuthResponseDto BuildAuthResponse(AppUser user)
    {
        var canIssueToken = user.IsActive && user.EmailConfirmed;
        var tokenValue = string.Empty;
        var expiresAtUtc = DateTime.UtcNow;
        if (canIssueToken)
        {
            var token = jwtTokenService.GenerateToken(user);
            tokenValue = token.Token;
            expiresAtUtc = token.ExpiresAtUtc;
        }

        return new AuthResponseDto
        {
            UserId = user.Id,
            CompanyId = user.CompanyId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = authRouteService.ToClientRole(user.Role),
            SystemRole = user.Role,
            RedirectUrl = authRouteService.GetHomePath(user.Role),
            EmailConfirmed = user.EmailConfirmed,
            ThemePreference = NormalizeThemePreference(user.ThemePreference),
            Token = tokenValue,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private static string NormalizeThemePreference(string? theme)
        => string.Equals(theme, "diamond-silver", StringComparison.OrdinalIgnoreCase)
            ? "diamond-silver"
            : "gold-premium";

    private static void ValidatePasswordStrength(string password)
    {
        if (password.Length < 8
            || !password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit))
        {
            throw new BusinessRuleException("Şifre en az 8 karakter olmalı ve büyük harf, küçük harf ve rakam içermelidir.");
        }
    }
}
