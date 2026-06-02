using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;

namespace MGold.Domain.Entities;

public class OrderHistoryEntry
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderHistoryType Type { get; set; }

    [Required]
    [MaxLength(140)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? ActorUsername { get; set; }

    [MaxLength(30)]
    public string? ActorRole { get; set; }

    [MaxLength(80)]
    public string? RelatedEntityName { get; set; }

    [MaxLength(80)]
    public string? RelatedEntityId { get; set; }

    [MaxLength(2000)]
    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
