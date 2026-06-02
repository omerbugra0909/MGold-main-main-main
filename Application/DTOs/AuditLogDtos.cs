namespace MGold.Application.DTOs;

public class CreateAuditLogDto
{
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? UserRole { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AuditLogDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? UserRole { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? ErrorMessage { get; set; }
}
