using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class ProductReviewsController(IProductReviewService productReviewService) : BaseApiController
{
    [HttpGet("product/{productId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByProduct(int productId, [FromQuery] bool includePending, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await productReviewService.GetByProductAsync(productId, includePending, cancellationToken), "Product reviews retrieved successfully.");

    [HttpGet("pending")]
    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await productReviewService.GetPendingAsync(cancellationToken), "Pending reviews retrieved successfully.");

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateProductReviewDto dto, CancellationToken cancellationToken)
    {
        var result = await productReviewService.CreateAsync(dto, cancellationToken);
        return ApiResponseFactory.Create(this, result, "Review submitted successfully.", StatusCodes.Status201Created);
    }

    [HttpPatch("{id:int}/moderate")]
    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    public async Task<IActionResult> Moderate(int id, [FromBody] ModerateProductReviewDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await productReviewService.ModerateAsync(id, dto, cancellationToken), "Review moderated successfully.");
}
