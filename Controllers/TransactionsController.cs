using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class TransactionsController(ITransactionService transactionService) : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = RoleConstants.TransactionReadRoles)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await transactionService.GetAllAsync(cancellationToken), "Transactions retrieved successfully.");

    [HttpGet("{id:int}")]
    [Authorize(Roles = RoleConstants.TransactionReadRoles)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await transactionService.GetByIdAsync(id, cancellationToken), "Transaction retrieved successfully.");

    [HttpPost]
    [Authorize(Roles = RoleConstants.TransactionWriteRoles)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionDto dto, CancellationToken cancellationToken)
    {
        var result = await transactionService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<object>.Ok(result, "Transaction created successfully."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleConstants.TransactionWriteRoles)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTransactionDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await transactionService.UpdateAsync(id, dto, cancellationToken), "Transaction updated successfully.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await transactionService.DeleteAsync(id, cancellationToken);
        return ApiResponseFactory.Create(this, new { DeletedId = id }, "Transaction deleted successfully.");
    }
}
