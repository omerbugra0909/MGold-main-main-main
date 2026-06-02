using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

[ApiController]
[Authorize(Roles = RoleConstants.OperationalTaskRoles)]
[Produces("application/json")]
[Consumes("application/json")]
[Route("api/workforce")]
public class WorkforceApiController(IWorkforceService workforceService) : ControllerBase
{
    [HttpGet("manager-dashboard")]
    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    public async Task<IActionResult> GetManagerDashboard(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.GetCompanyDashboardAsync(cancellationToken), "Workforce dashboard retrieved successfully.");

    [HttpGet("employee-workspace")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Employee}")]
    public async Task<IActionResult> GetEmployeeWorkspace(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.GetEmployeeWorkspaceAsync(cancellationToken), "Employee workspace retrieved successfully.");

    [HttpPost("tasks")]
    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    public async Task<IActionResult> AssignTask([FromBody] CreateTaskDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.AssignTaskAsync(dto, cancellationToken), "Task assigned successfully.", StatusCodes.Status201Created);

    [HttpPatch("tasks/{taskId:int}/status")]
    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Employee}")]
    public async Task<IActionResult> UpdateTaskStatus(int taskId, [FromBody] UpdateTaskStatusDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.UpdateTaskStatusAsync(taskId, dto, cancellationToken), "Task status updated successfully.");
}

