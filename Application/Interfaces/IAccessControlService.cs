namespace MGold.Application.Interfaces;

public interface IAccessControlService
{
    void EnsureCanReadOperationalData();
    void EnsureCanWriteOperationalData();
    void EnsureCanDeleteData();
    void EnsureAdminOnly();
    void EnsureSystemAdminOnly();
    void EnsureManagerOrSystemAdmin();
    void EnsureEmployeeWorkspaceAccess();
    void EnsureSameCompany(int? companyId);
}
