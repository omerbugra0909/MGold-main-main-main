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

        throw new AuthorizationException("Bu işlem için firma bağlamina sahip yetkili erişim gerekiyor.");
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

        throw new AuthorizationException("Bu işlem için firma bağlamina sahip yazma yetkisi gerekiyor.");
    }

    public void EnsureCanDeleteData()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || (currentUserService.IsInRole(RoleConstants.Manager) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Silme işlemi için yönetici erişimi gerekiyor.");
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

        throw new AuthorizationException("Bu işlem yalnızca sistem adminine açıktir.");
    }

    public void EnsureManagerOrSystemAdmin()
    {
        EnsureAuthenticated();
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || (currentUserService.IsInRole(RoleConstants.Manager) && currentUserService.CompanyId.HasValue))
        {
            return;
        }

        throw new AuthorizationException("Bu işlem için firma yöneticisi veya sistem admini olmaniz gerekiyor.");
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

        throw new AuthorizationException("Bu alan yalnızca ic ekip kullanıcılarina açıktir.");
    }

    public void EnsureSameCompany(int? companyId)
    {
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            return;
        }

        if (!currentUserService.CompanyId.HasValue || !companyId.HasValue || currentUserService.CompanyId != companyId)
        {
            throw new AuthorizationException("Bu kayda farkli bir firma bağlamindan erisilemez.");
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
