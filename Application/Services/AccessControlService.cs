using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Application.Services;

public class AccessControlService(ICurrentUserService currentUserService) : IAccessControlService
{
    public void EnsureCanReadOperationalData()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || (currentUserService.IsInRole(RoleConstants.Manager) && currentUserService.CompanyId.HasValue)
            || (currentUserService.IsInRole(RoleConstants.Employee) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Bu islem icin firma baglamina sahip yetkili erisim gerekiyor.");
    }

    public void EnsureCanWriteOperationalData()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || (currentUserService.IsInRole(RoleConstants.Manager) && currentUserService.CompanyId.HasValue)
            || (currentUserService.IsInRole(RoleConstants.Employee) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Bu islem icin firma baglamina sahip yazma yetkisi gerekiyor.");
    }

    public void EnsureCanDeleteData()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || (currentUserService.IsInRole(RoleConstants.Manager) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Silme islemi icin yonetici erisimi gerekiyor.");
    }

    public void EnsureAdminOnly()
        => EnsureSystemAdminOnly();

    public void EnsureSystemAdminOnly()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            return;
        }

        throw new AuthorizationException("Bu islem yalnizca sistem adminine aciktir.");
    }

    public void EnsureManagerOrSystemAdmin()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || (currentUserService.IsInRole(RoleConstants.Manager) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Bu islem icin firma yoneticisi veya sistem admini olmaniz gerekiyor.");
    }

    public void EnsureEmployeeWorkspaceAccess()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || currentUserService.IsInRole(RoleConstants.Manager)
            || (currentUserService.IsInRole(RoleConstants.Employee) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Bu alan yalnizca ic ekip kullanicilarina aciktir.");
    }

    public void EnsureSameCompany(int? companyId)
    {
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            return;
        }

        if (!currentUserService.CompanyId.HasValue || !companyId.HasValue || currentUserService.CompanyId != companyId)
        {
            throw new AuthorizationException("Bu kayda farkli bir firma baglamindan erisilemez.");
        }
    }

    private void EnsureAuthenticated()
    {
        if (!currentUserService.IsAuthenticated)
        {
            throw new AuthorizationException("Authentication is required for this action.");
        }
    }
}
