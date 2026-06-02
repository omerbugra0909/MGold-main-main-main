using System.Security.Claims;

namespace MGold.Application.Interfaces;

public interface IAuthRouteService
{
    string ToClientRole(string systemRole);
    string GetHomePath(string systemRole);
    bool CanAccessPath(ClaimsPrincipal user, PathString path);
}
