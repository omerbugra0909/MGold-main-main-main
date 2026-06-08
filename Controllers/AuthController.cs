using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;
using MGold.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MGold.Controllers;

public class AuthController(
    IAuthService authService,
    IAccountVerificationService accountVerificationService,
    IRefreshTokenService refreshTokenService,
    AppDbContext context,
    ICurrentUserService currentUserService,
    IConfiguration configuration) : BaseApiController
{
    [AllowAnonymous]
    [HttpGet("bootstrap-status")]
    public async Task<IActionResult> GetBootstrapStatus(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await authService.GetBootstrapStatusAsync(cancellationToken), "Authentication bootstrap status retrieved successfully.");

    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(dto, cancellationToken);
        await accountVerificationService.SendEmailConfirmationAsync(
            result.Email,
            GetBaseUrl(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return ApiResponseFactory.Create(this, result, "User registered successfully.", StatusCodes.Status201Created);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await authService.LoginAsync(dto, cancellationToken), "Login successful.");

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(
            this,
            await refreshTokenService.RefreshAsync(
                dto.RefreshToken,
                dto.DeviceName,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken),
            "Token refreshed successfully.");

    [AllowAnonymous]
    [HttpPost("revoke-refresh-token")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RevokeRefreshToken([FromBody] RevokeRefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(dto.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return ApiResponseFactory.Create(this, new { revoked = true }, "Refresh token revoked successfully.");
    }

    [HttpGet("sessions")]
    [Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await refreshTokenService.GetSessionsForCurrentUserAsync(cancellationToken), "Auth sessions retrieved successfully.");

    [HttpPost("logout")]
    [Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is int userId)
        {
            await refreshTokenService.RevokeAllForUserAsync(userId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        }

        return ApiResponseFactory.Create(this, new { loggedOut = true }, "All refresh sessions revoked successfully.");
    }

    [AllowAnonymous]
    [HttpPost("resend-email-verification")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendEmailVerification([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        await accountVerificationService.SendEmailConfirmationAsync(
            dto.Identifier,
            GetBaseUrl(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return ApiResponseFactory.Create(this, new { sent = true }, "Verification email request accepted.");
    }

    [AllowAnonymous]
    [HttpPost("resend-phone-verification")]
    [EnableRateLimiting("sms")]
    public async Task<IActionResult> ResendPhoneVerification([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        await accountVerificationService.SendPhoneConfirmationAsync(
            dto.Identifier,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return ApiResponseFactory.Create(this, new { sent = true }, "Verification SMS request accepted.");
    }

    [AllowAnonymous]
    [HttpPost("verify-phone")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequestDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await accountVerificationService.ConfirmPhoneAsync(dto, cancellationToken), "Phone verification completed.");

    [AllowAnonymous]
    [HttpGet("verify-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail([FromQuery] int userId, [FromQuery] string token, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await accountVerificationService.ConfirmEmailAsync(userId, token, cancellationToken), "Email verification completed.");

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        await accountVerificationService.StartPasswordResetAsync(
            dto,
            GetBaseUrl(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return ApiResponseFactory.Create(this, new { sent = true }, "Password reset request accepted.");
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await accountVerificationService.ResetPasswordAsync(dto, cancellationToken), "Password reset completed.");

    [Authorize(Roles = RoleConstants.AdminOnly)]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await authService.GetUsersAsync(cancellationToken), "Users retrieved successfully.");

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPatch("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequestDto dto, CancellationToken cancellationToken)
    {
        var user = await context.AppUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"User with id {id} was not found.");

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin) && user.CompanyId != currentUserService.CompanyId)
        {
            return ApiResponseFactory.CreateFailure(this, "Forbidden.", StatusCodes.Status403Forbidden, "Bu kullanıcıyi güncelleme yetkiniz yok.");
        }

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin)
            && (!string.Equals(user.Role, RoleConstants.Employee, StringComparison.Ordinal)
                || !string.Equals(dto.Role, RoleConstants.Employee, StringComparison.Ordinal)))
        {
            return ApiResponseFactory.CreateFailure(this, "Forbidden.", StatusCodes.Status403Forbidden, "Firma yöneticisi yalnızca kendi firmasindaki çalışan hesaplarıni yönetebilir.");
        }

        if (string.Equals(User.Identity?.Name, user.Username, StringComparison.OrdinalIgnoreCase) && !dto.IsActive)
        {
            return ApiResponseFactory.CreateFailure(this, "Validation failed.", StatusCodes.Status400BadRequest, "Kendi hesabınızı pasife alamazsiniz.");
        }

        if (!RoleConstants.All.Contains(dto.Role))
        {
            return ApiResponseFactory.CreateFailure(this, "Validation failed.", StatusCodes.Status400BadRequest, "Geçersiz rol secimi.");
        }

        user.Role = dto.Role;
        user.IsActive = dto.IsActive;
        await context.SaveChangesAsync(cancellationToken);

        var result = new UserInfoDto
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            CustomerId = user.CustomerId,
            ThemePreference = user.ThemePreference,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
        return ApiResponseFactory.Create(this, result, "User updated successfully.");
    }

    private string GetBaseUrl()
    {
        var configuredBaseUrl = configuration["App:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? Request.Scheme : forwardedProto;
        return $"{scheme}://{Request.Host}";
    }
}
