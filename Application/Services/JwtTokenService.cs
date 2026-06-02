using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Entities;

namespace MGold.Application.Services;

public class JwtTokenService(IOptions<JwtSettings> settings) : IJwtTokenService
{
    public (string Token, DateTime ExpiresAtUtc) GenerateToken(AppUser user)
    {
        var jwtSettings = settings.Value;
        var expires = DateTime.UtcNow.AddMinutes(jwtSettings.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("client_role", ToClientRole(user.Role)),
            new Claim("company_id", user.CompanyId?.ToString() ?? string.Empty),
            new Claim("firm_id", user.CompanyId?.ToString() ?? string.Empty),
            new Claim("security_stamp", user.SecurityStamp),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return (encoded, expires);
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
}
