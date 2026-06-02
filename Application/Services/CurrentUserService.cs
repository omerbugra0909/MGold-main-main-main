using System.Security.Claims;
using MGold.Application.Interfaces;

namespace MGold.Application.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public int? UserId
    {
        get
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public int? CompanyId
    {
        get
        {
            var raw = User?.FindFirstValue("firm_id") ?? User?.FindFirstValue("company_id");
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);
    public string? FullName => User?.FindFirstValue(ClaimTypes.GivenName);

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);

    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}
