using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class DashboardController(IDashboardService dashboardService) : BaseApiController
{
    [HttpGet("summary")]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetSummary(string? rangePreset, DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(
            this,
            await dashboardService.GetSummaryAsync(rangePreset, startDate, endDate, cancellationToken),
            "Dashboard summary retrieved successfully.");
}
