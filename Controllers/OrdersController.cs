using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class OrdersController(IOrderService orderService) : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await orderService.GetAllAsync(cancellationToken), "Orders retrieved successfully.");

    [HttpGet("{id:int}")]
    [Authorize(Roles = RoleConstants.BackOfficeReadRoles)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await orderService.GetByIdAsync(id, cancellationToken), "Order retrieved successfully.");

    [HttpPost]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        var result = await orderService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<object>.Ok(result, "Order created successfully."));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await orderService.UpdateStatusAsync(id, dto, cancellationToken), "Order status updated successfully.");

    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    [EnableRateLimiting("payment")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] CreateOrderPaymentDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await orderService.AddPaymentAsync(id, dto, cancellationToken), "Order payment added successfully.");
}
