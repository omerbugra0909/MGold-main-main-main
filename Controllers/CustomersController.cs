using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class CustomersController(ICustomerService customerService) : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await customerService.GetAllAsync(cancellationToken), "Customers retrieved successfully.");

    [HttpGet("{id:int}")]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await customerService.GetByIdAsync(id, cancellationToken), "Customer retrieved successfully.");

    [HttpPost]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto, CancellationToken cancellationToken)
    {
        var result = await customerService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<object>.Ok(result, "Customer created successfully."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await customerService.UpdateAsync(id, dto, cancellationToken), "Customer updated successfully.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await customerService.DeleteAsync(id, cancellationToken);
        return ApiResponseFactory.Create(this, new { DeletedId = id }, "Customer deleted successfully.");
    }
}
