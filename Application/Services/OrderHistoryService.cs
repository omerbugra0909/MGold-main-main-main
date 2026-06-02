using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class OrderHistoryService(
    AppDbContext context,
    ICurrentUserService currentUserService,
    IAccessControlService accessControlService) : IOrderHistoryService
{
    public async Task RecordAsync(
        int orderId,
        OrderHistoryType type,
        string title,
        string description,
        string? relatedEntityName = null,
        string? relatedEntityId = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new OrderHistoryEntry
        {
            OrderId = orderId,
            Type = type,
            Title = title.Trim(),
            Description = description.Trim(),
            ActorUsername = currentUserService.Username,
            ActorRole = currentUserService.Role,
            RelatedEntityName = relatedEntityName,
            RelatedEntityId = relatedEntityId,
            MetadataJson = metadataJson,
            CreatedAt = DateTime.UtcNow
        };

        await context.OrderHistoryEntries.AddAsync(entry, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderHistoryEntryDto>> GetForOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        return await context.OrderHistoryEntries
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OrderHistoryEntryDto
            {
                Id = x.Id,
                Type = x.Type,
                Title = x.Title,
                Description = x.Description,
                ActorUsername = x.ActorUsername,
                ActorRole = x.ActorRole,
                RelatedEntityName = x.RelatedEntityName,
                RelatedEntityId = x.RelatedEntityId,
                MetadataJson = x.MetadataJson,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
