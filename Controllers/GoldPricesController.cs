using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class GoldPricesController(IGoldPriceService goldPriceService) : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await goldPriceService.GetAllAsync(cancellationToken), "Gold price history retrieved successfully.");

    [HttpGet("latest")]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await goldPriceService.GetLatestAsync(cancellationToken), "Latest gold price retrieved successfully.");

    [HttpGet("{id:int}")]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await goldPriceService.GetByIdAsync(id, cancellationToken), "Gold price retrieved successfully.");

    [HttpPost]
    [Authorize(Roles = RoleConstants.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] CreateGoldPriceDto dto, CancellationToken cancellationToken)
    {
        var result = await goldPriceService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<object>.Ok(result, "Gold price created successfully."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOnly)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGoldPriceDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await goldPriceService.UpdateAsync(id, dto, cancellationToken), "Gold price updated successfully.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await goldPriceService.DeleteAsync(id, cancellationToken);
        return ApiResponseFactory.Create(this, new { DeletedId = id }, "Gold price deleted successfully.");
    }
}
