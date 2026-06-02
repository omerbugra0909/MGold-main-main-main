using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class NotificationsController(INotificationService notificationService) : BaseApiController
{
    [HttpGet]
    [Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
    public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await notificationService.GetForCurrentUserAsync(unreadOnly, cancellationToken), "Notifications retrieved successfully.");

    [HttpPost]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationDto dto, CancellationToken cancellationToken)
        => ApiResponseFactory.Create(this, await notificationService.CreateAsync(dto, cancellationToken), "Notification created successfully.", StatusCodes.Status201Created);

    [HttpPost("generate-low-stock")]
    [Authorize(Roles = RoleConstants.BackOfficeWriteRoles)]
    public async Task<IActionResult> GenerateLowStock(CancellationToken cancellationToken)
    {
        await notificationService.GenerateLowStockNotificationsAsync(cancellationToken);
        return ApiResponseFactory.Create(this, new { generated = true }, "Low stock notifications generated successfully.");
    }

    [HttpPatch("{id:int}/read")]
    [Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        await notificationService.MarkAsReadAsync(id, cancellationToken);
        return ApiResponseFactory.Create(this, new { id }, "Notification marked as read.");
    }
}
