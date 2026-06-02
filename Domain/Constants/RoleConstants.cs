namespace MGold.Domain.Constants;

public static class RoleConstants
{
    public const string SystemAdmin = "SystemAdmin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string Customer = "Customer";

    public const string Admin = SystemAdmin;
    public const string ShopOwner = Manager;
    public const string User = Customer;

    public static readonly string[] All = [SystemAdmin, Manager, Employee, Customer];
    public const string SystemAdminOnly = SystemAdmin;
    public const string AdminOnly = SystemAdminOnly;
    public const string ManagerOnly = Manager;
    public const string ShopOwnerOnly = ShopOwner;
    public const string EmployeeOnly = Employee;
    public const string CustomerOnly = Customer;
    public const string InternalPortalRoles = $"{SystemAdmin},{Manager},{Employee}";
    public const string ManagerOrSystemAdmin = $"{SystemAdmin},{Manager}";
    public const string ShopOwnerOrSystemAdmin = $"{SystemAdmin},{ShopOwner}";
    public const string EmployeeOrManagerOrSystemAdmin = $"{SystemAdmin},{Manager},{Employee}";
    public const string StoreStaffRoles = $"{SystemAdmin},{Manager},{Employee}";
    public const string AuthenticatedPortalRoles = $"{SystemAdmin},{Manager},{Employee},{Customer}";
    public const string BackOfficeReadRoles = $"{SystemAdmin},{Manager}";
    public const string BackOfficeWriteRoles = $"{SystemAdmin},{Manager}";
    public const string TransactionReadRoles = StoreStaffRoles;
    public const string TransactionWriteRoles = StoreStaffRoles;
    public const string ReviewModerationRoles = $"{SystemAdmin},{Manager}";
    public const string OperationalTaskRoles = $"{SystemAdmin},{Manager},{Employee}";
}
