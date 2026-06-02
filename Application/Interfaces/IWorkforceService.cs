using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IWorkforceService
{
    Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken cancellationToken = default);
    Task<CompanyWorkspaceDashboardDto> GetCompanyDashboardAsync(CancellationToken cancellationToken = default);
    Task<EmployeeWorkspaceDto> GetEmployeeWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<CompanySummaryDto> CreateCompanyAsync(CreateCompanyDto dto, CancellationToken cancellationToken = default);
    Task<UserInfoDto> CreateInternalUserAsync(CreateInternalUserDto dto, CancellationToken cancellationToken = default);
    Task<TaskCardDto> AssignTaskAsync(CreateTaskDto dto, CancellationToken cancellationToken = default);
    Task<TaskCardDto> UpdateTaskStatusAsync(int taskId, UpdateTaskStatusDto dto, CancellationToken cancellationToken = default);
}
