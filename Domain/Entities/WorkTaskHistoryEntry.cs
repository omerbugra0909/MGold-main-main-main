using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;
using TaskState = MGold.Domain.Enums.TaskStatus;

namespace MGold.Domain.Entities;

public class WorkTaskHistoryEntry
{
    public int Id { get; set; }

    public int WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;

    [Required]
    [MaxLength(140)]
    public string ActionTitle { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string? Description { get; set; }

    public TaskState? PreviousStatus { get; set; }
    public TaskState NewStatus { get; set; }

    public int ActorUserId { get; set; }
    public AppUser ActorUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
