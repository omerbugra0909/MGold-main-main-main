using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Infrastructure.Data;

namespace MGold.Controllers;

[ApiController]
public class ThemeController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gold-premium",
        "diamond-silver"
    };

    [AllowAnonymous]
    [HttpPost("/api/theme/preference")]
    public async Task<IActionResult> SavePreference([FromBody] ThemePreferenceRequest request, CancellationToken cancellationToken)
    {
        var theme = NormalizeTheme(request.Theme);
        Response.Cookies.Append("MGold.Theme", theme, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        });

        if (User.Identity?.IsAuthenticated == true
            && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var user = await db.AppUsers.FindAsync([userId], cancellationToken);
            if (user is not null && !string.Equals(user.ThemePreference, theme, StringComparison.OrdinalIgnoreCase))
            {
                user.ThemePreference = theme;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(new
        {
            theme,
            name = theme == "diamond-silver" ? "Diamond Silver" : "Gold Premium"
        });
    }

    private static string NormalizeTheme(string? theme)
    {
        var normalized = string.IsNullOrWhiteSpace(theme)
            ? "gold-premium"
            : theme.Trim().ToLowerInvariant();

        return AllowedThemes.Contains(normalized) ? normalized : "gold-premium";
    }
}

public class ThemePreferenceRequest
{
    [MaxLength(40)]
    public string? Theme { get; set; }
}
