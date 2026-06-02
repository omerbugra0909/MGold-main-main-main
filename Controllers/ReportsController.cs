using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class ReportsController(IReportService reportService) : BaseApiController
{
    [HttpGet("profit-loss")]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetProfitLossSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(
            this,
            await reportService.GetProfitLossSummaryAsync(startDate, endDate, cancellationToken),
            "Profit/loss summary retrieved successfully.");
}
