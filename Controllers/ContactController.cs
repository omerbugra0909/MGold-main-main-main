using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class ContactController(IContactService contactService) : BaseApiController
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Submit([FromBody] SubmitContactMessageDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await contactService.SubmitAsync(dto, cancellationToken), "Contact message submitted successfully.", StatusCodes.Status201Created);

    [HttpGet]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await contactService.GetAllAsync(cancellationToken), "Contact messages retrieved successfully.");

    [HttpPatch("{id:int}/resolve")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveContactMessageDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await contactService.ResolveAsync(id, dto, cancellationToken), "Contact message resolved successfully.");
}
