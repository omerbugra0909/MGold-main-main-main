namespace MGold.Application.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    int? CompanyId { get; }
    string? Username { get; }
    string? FullName { get; }
    string? Role { get; }
    bool IsInRole(string role);
}
