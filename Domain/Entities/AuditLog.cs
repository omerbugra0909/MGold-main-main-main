using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    [MaxLength(80)]
    public string? Username { get; set; }

    [MaxLength(30)]
    public string? UserRole { get; set; }

    [Required]
    [MaxLength(20)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityId { get; set; }

    [Required]
    [MaxLength(10)]
    public string HttpMethod { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? ErrorMessage { get; set; }
}
