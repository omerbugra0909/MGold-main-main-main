using System.Security.Claims;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Application.Services;

public class AuthRouteService : IAuthRouteService
{
    public string ToClientRole(string systemRole)
        => systemRole switch
        {
            RoleConstants.SystemAdmin => "admin",
            RoleConstants.Manager => "shopOwner",
            RoleConstants.Employee => "employee",
            RoleConstants.Customer => "customer",
            _ => "guest"
        };

    public string GetHomePath(string systemRole)
        => systemRole switch
        {
            RoleConstants.SystemAdmin => "/admin",
            RoleConstants.Manager => "/owner",
            RoleConstants.Employee => "/employee",
            RoleConstants.Customer => "/home",
            _ => "/auth"
        };

    public bool CanAccessPath(ClaimsPrincipal user, PathString path)
    {
        var value = path.Value?.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Equals("/admin/login", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/workspace/login", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/account/logout", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("/admin", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/platform", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/platform/", StringComparison.OrdinalIgnoreCase))
        {
            return user.IsInRole(RoleConstants.SystemAdmin);
        }

        if (value.Equals("/owner", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/owner/", StringComparison.OrdinalIgnoreCase))
        {
            return user.IsInRole(RoleConstants.Manager);
        }

        if (value.Equals("/employee", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/employee/", StringComparison.OrdinalIgnoreCase))
        {
            return user.IsInRole(RoleConstants.Employee);
        }

        if (value.Equals("/home", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/home/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/customer", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/customer/", StringComparison.OrdinalIgnoreCase))
        {
            return user.IsInRole(RoleConstants.Customer);
        }

        if (value.Equals("/workspace", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/workspace/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
