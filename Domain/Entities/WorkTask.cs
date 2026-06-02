using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;
using TaskState = MGold.Domain.Enums.TaskStatus;

namespace MGold.Domain.Entities;

public class WorkTask
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [Required]
    [MaxLength(140)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskState Status { get; set; } = TaskState.Waiting;
    public DateTime? DueDate { get; set; }

    public int AssignedToUserId { get; set; }
    public AppUser AssignedToUser { get; set; } = null!;

    public int AssignedByUserId { get; set; }
    public AppUser AssignedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<WorkTaskHistoryEntry> HistoryEntries { get; set; } = new List<WorkTaskHistoryEntry>();
}
