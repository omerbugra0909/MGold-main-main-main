using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class CreateNotificationDto
{
    [Required]
    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1200)]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.Info;

    [MaxLength(30)]
    public string? TargetRole { get; set; }

    [MaxLength(80)]
    public string? RelatedEntityName { get; set; }

    [MaxLength(80)]
    public string? RelatedEntityId { get; set; }

    public bool IsCritical { get; set; }
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string? TargetRole { get; set; }
    public string? RelatedEntityName { get; set; }
    public string? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public bool IsCritical { get; set; }
    public DateTime CreatedAt { get; set; }
}
