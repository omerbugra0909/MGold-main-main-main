using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Middleware;

public class AuthRouteGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuthRouteService authRouteService)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && !IsApiOrAssetRequest(context.Request.Path)
            && TryResolveLegacyAdminPath(context.Request.Path, user, authRouteService, out var legacyRedirect))
        {
            context.Response.Redirect(legacyRedirect);
            return;
        }

        if (user.Identity?.IsAuthenticated == true
            && !IsApiOrAssetRequest(context.Request.Path)
            && !authRouteService.CanAccessPath(user, context.Request.Path))
        {
            var role = ResolveSystemRole(user);
            context.Response.Redirect(authRouteService.GetHomePath(role));
            return;
        }

        await next(context);
    }

    private static bool TryResolveLegacyAdminPath(
        PathString path,
        System.Security.Claims.ClaimsPrincipal user,
        IAuthRouteService authRouteService,
        out string redirectPath)
    {
        var value = path.Value ?? string.Empty;
        if (!value.StartsWith("/Admin", StringComparison.Ordinal))
        {
            redirectPath = string.Empty;
            return false;
        }

        redirectPath = authRouteService.GetHomePath(ResolveSystemRole(user));
        return true;
    }

    private static bool IsApiOrAssetRequest(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSystemRole(System.Security.Claims.ClaimsPrincipal user)
    {
        if (user.IsInRole(RoleConstants.SystemAdmin))
        {
            return RoleConstants.SystemAdmin;
        }

        if (user.IsInRole(RoleConstants.Manager))
        {
            return RoleConstants.Manager;
        }

        if (user.IsInRole(RoleConstants.Employee))
        {
            return RoleConstants.Employee;
        }

        return RoleConstants.Customer;
    }
}
