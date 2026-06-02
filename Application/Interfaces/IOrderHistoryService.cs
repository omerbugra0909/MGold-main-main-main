using MGold.Application.DTOs;
using MGold.Domain.Enums;

namespace MGold.Application.Interfaces;

public interface IOrderHistoryService
{
    Task RecordAsync(
        int orderId,
        OrderHistoryType type,
        string title,
        string description,
        string? relatedEntityName = null,
        string? relatedEntityId = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderHistoryEntryDto>> GetForOrderAsync(int orderId, CancellationToken cancellationToken = default);
}
