using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Constants;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class NotificationService(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IAccessControlService accessControlService,
    IEmailService emailService,
    IOptions<EmailSettings> emailOptions) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var query = context.Notifications
            .AsNoTracking()
            .Where(x => x.TargetRole == null || x.TargetRole == currentUserService.Role);

        query = await ApplyCompanyScopeAsync(query, cancellationToken);

        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<NotificationDto> CreateAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        var normalizedTargetRole = NormalizeTargetRole(dto.TargetRole);

        var entity = new Domain.Entities.Notification
        {
            Title = dto.Title.Trim(),
            Message = dto.Message.Trim(),
            Type = dto.Type,
            TargetRole = normalizedTargetRole,
            RelatedEntityName = string.IsNullOrWhiteSpace(dto.RelatedEntityName) ? null : dto.RelatedEntityName.Trim(),
            RelatedEntityId = string.IsNullOrWhiteSpace(dto.RelatedEntityId) ? null : dto.RelatedEntityId.Trim(),
            IsCritical = dto.IsCritical,
            CreatedAt = DateTime.UtcNow
        };

        await context.Notifications.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (entity.IsCritical || entity.TargetRole == RoleConstants.SystemAdmin)
        {
            await SendAdminEmailAsync(entity.Title, entity.Message, cancellationToken);
        }

        return entity.ToDto();
    }

    public async Task MarkAsReadAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Notifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Notification with id {id} was not found.");

        if (!string.IsNullOrWhiteSpace(entity.TargetRole) && entity.TargetRole != currentUserService.Role)
        {
            throw new AuthorizationException("You are not allowed to update this notification.");
        }

        if (!await CanAccessByCompanyScopeAsync(entity, cancellationToken))
        {
            throw new AuthorizationException("You are not allowed to update this notification.");
        }

        entity.IsRead = true;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task GenerateLowStockNotificationsAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();

        var lowStockProducts = await context.Products
            .AsNoTracking()
            .Where(x => x.StockQuantity <= 5)
            .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.CompanyId == currentUserService.CompanyId)
            .ToListAsync(cancellationToken);

        foreach (var product in lowStockProducts)
        {
            var relatedId = product.Id.ToString();
            var exists = await context.Notifications.AnyAsync(x =>
                x.Type == Domain.Enums.NotificationType.LowStock &&
                x.RelatedEntityName == nameof(Domain.Entities.Product) &&
                x.RelatedEntityId == relatedId &&
                !x.IsRead, cancellationToken);

            if (exists)
            {
                continue;
            }

            await context.Notifications.AddAsync(new Domain.Entities.Notification
            {
                Title = "Düşük stok uyarisi",
                Message = $"{product.Name} için stok seviyesi {product.StockQuantity} adede düştü.",
                Type = NotificationType.LowStock,
                TargetRole = RoleConstants.Manager,
                RelatedEntityName = nameof(Domain.Entities.Product),
                RelatedEntityId = relatedId,
                IsCritical = product.StockQuantity == 0,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await SendAdminEmailAsync(
                $"Düşük stok uyarisi: {product.Name}",
                $"{product.Name} için stok seviyesi {product.StockQuantity} adede düştü.",
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SendAdminEmailAsync(string subject, string message, CancellationToken cancellationToken)
    {
        var supportInbox = emailOptions.Value.SupportInbox;
        if (string.IsNullOrWhiteSpace(supportInbox))
        {
            return;
        }

        await emailService.SendAsync(new SendEmailRequestDto
        {
            To = supportInbox,
            Subject = subject,
            HtmlBody = $"<p>{message}</p>"
        }, cancellationToken);
    }

    private static string? NormalizeTargetRole(string? targetRole)
    {
        if (string.IsNullOrWhiteSpace(targetRole))
        {
            return null;
        }

        var normalized = targetRole.Trim();
        if (!RoleConstants.All.Contains(normalized, StringComparer.Ordinal))
        {
            throw new BusinessRuleException("Geçersiz hedef rol belirtildi.");
        }

        return normalized;
    }

    private async Task<IQueryable<Domain.Entities.Notification>> ApplyCompanyScopeAsync(
        IQueryable<Domain.Entities.Notification> query,
        CancellationToken cancellationToken)
    {
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin) || currentUserService.CompanyId is not int companyId)
        {
            return query;
        }

        var orderIds = await context.Orders
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);
        var productIds = await context.Products
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);
        var reviewIds = await context.ProductReviews
            .AsNoTracking()
            .Where(x => x.Product.CompanyId == companyId)
            .Select(x => x.Id.ToString())
            .ToListAsync(cancellationToken);

        return query.Where(x =>
            x.RelatedEntityName == null
            || (x.RelatedEntityName == nameof(Domain.Entities.Order) && x.RelatedEntityId != null && orderIds.Contains(x.RelatedEntityId))
            || (x.RelatedEntityName == nameof(Domain.Entities.Product) && x.RelatedEntityId != null && productIds.Contains(x.RelatedEntityId))
            || (x.RelatedEntityName == nameof(Domain.Entities.ProductReview) && x.RelatedEntityId != null && reviewIds.Contains(x.RelatedEntityId)));
    }

    private async Task<bool> CanAccessByCompanyScopeAsync(Domain.Entities.Notification notification, CancellationToken cancellationToken)
    {
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin) || currentUserService.CompanyId is not int companyId)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(notification.RelatedEntityName) || string.IsNullOrWhiteSpace(notification.RelatedEntityId))
        {
            return true;
        }

        if (!int.TryParse(notification.RelatedEntityId, out var relatedId))
        {
            return false;
        }

        return notification.RelatedEntityName switch
        {
            nameof(Domain.Entities.Order) => await context.Orders.AnyAsync(x => x.Id == relatedId && x.CompanyId == companyId, cancellationToken),
            nameof(Domain.Entities.Product) => await context.Products.AnyAsync(x => x.Id == relatedId && x.CompanyId == companyId, cancellationToken),
            nameof(Domain.Entities.ProductReview) => await context.ProductReviews.AnyAsync(x => x.Id == relatedId && x.Product.CompanyId == companyId, cancellationToken),
            _ => false
        };
    }
}
