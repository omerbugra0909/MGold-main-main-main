using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

[ApiController]
[Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
[Produces("application/json")]
[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoiceService) : ControllerBase
{
    [HttpPost("orders/{orderId:int}")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> GenerateForOrder(int orderId, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await invoiceService.GenerateForOrderAsync(orderId, cancellationToken), "Invoice generated successfully.", StatusCodes.Status201Created);

    [HttpGet("{invoiceId:int}/download")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Download(int invoiceId, CancellationToken cancellationToken)
    {
        var pdf = await invoiceService.GetPdfAsync(invoiceId, cancellationToken);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }
}
