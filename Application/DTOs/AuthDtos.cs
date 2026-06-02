using System.ComponentModel.DataAnnotations;
using MGold.Domain.Constants;

namespace MGold.Application.DTOs;

public class RegisterRequestDto
{
    [Required]
    [MaxLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(30)]
    [RegularExpression(@"^(\+90|90|0)?5\d{9}$", ErrorMessage = "Gecerli bir telefon numarasi giriniz.")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;

    [RegularExpression($"^({RoleConstants.SystemAdmin}|{RoleConstants.Manager}|{RoleConstants.Employee}|{RoleConstants.Customer})$")]
    public string Role { get; set; } = RoleConstants.Customer;
}

public class LoginRequestDto
{
    [Required]
    [MaxLength(150)]
    public string EmailOrUsername { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;

    public bool RequireAdminPortal { get; set; }

    [MaxLength(160)]
    public string? DeviceName { get; set; }
}

public class AuthResponseDto
{
    public int UserId { get; set; }
    public int? CompanyId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SystemRole { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool RequiresEmailVerification => !EmailConfirmed;
    public string ThemePreference { get; set; } = "gold-premium";
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
}

public class AuthBootstrapStatusDto
{
    public bool HasUsers { get; set; }
    public bool RequiresSetup { get; set; }
}

public class UserInfoDto
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? CustomerId { get; set; }
    public string ThemePreference { get; set; } = "gold-premium";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UpdateUserRequestDto
{
    [Required]
    [RegularExpression($"^({RoleConstants.SystemAdmin}|{RoleConstants.Manager}|{RoleConstants.Employee}|{RoleConstants.Customer})$")]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class RefreshTokenRequestDto
{
    [Required]
    [MaxLength(500)]
    public string RefreshToken { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? DeviceName { get; set; }
}

public class RevokeRefreshTokenRequestDto
{
    [Required]
    [MaxLength(500)]
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthSessionDto
{
    public int Id { get; set; }
    public string? DeviceName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; }
}
