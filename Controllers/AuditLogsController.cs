using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class AuditLogsController(IAuditLogService auditLogService) : BaseApiController
{
    [Authorize(Roles = RoleConstants.SystemAdminOnly)]
    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int take = 100, CancellationToken cancellationToken = default)
        => ApiResponseFactory.Create(this, await auditLogService.GetRecentAsync(take, cancellationToken), "Audit logs retrieved successfully.");
}
