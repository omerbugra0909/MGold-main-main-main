using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForCurrentUserAsync(bool unreadOnly, CancellationToken cancellationToken = default);
    Task<NotificationDto> CreateAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(int id, CancellationToken cancellationToken = default);
    Task GenerateLowStockNotificationsAsync(CancellationToken cancellationToken = default);
}
