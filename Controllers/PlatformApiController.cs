using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

[ApiController]
[Authorize(Roles = RoleConstants.SystemAdminOnly)]
[Produces("application/json")]
[Consumes("application/json")]
[Route("api/platform")]
public class PlatformApiController(IWorkforceService workforceService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.GetPlatformDashboardAsync(cancellationToken), "Platform dashboard retrieved successfully.");

    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.CreateCompanyAsync(dto, cancellationToken), "Company created successfully.", StatusCodes.Status201Created);

    [HttpPost("users")]
    public async Task<IActionResult> CreateInternalUser([FromBody] CreateInternalUserDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await workforceService.CreateInternalUserAsync(dto, cancellationToken), "Internal user created successfully.", StatusCodes.Status201Created);
}

