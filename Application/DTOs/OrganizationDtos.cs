using System.ComponentModel.DataAnnotations;
using MGold.Domain.Enums;
using TaskState = MGold.Domain.Enums.TaskStatus;

namespace MGold.Application.DTOs;

public class PlatformDashboardDto
{
    public int TotalCompanies { get; set; }
    public int ActiveCompanies { get; set; }
    public int TotalInternalUsers { get; set; }
    public int TotalCustomers { get; set; }
    public IReadOnlyList<CompanySummaryDto> Companies { get; set; } = [];
    public IReadOnlyList<UserInfoDto> RecentUsers { get; set; } = [];
}

public class CompanySummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public int ManagerCount { get; set; }
    public int EmployeeCount { get; set; }
    public int CustomerCount { get; set; }
    public int ProductCount { get; set; }
    public int OpenOrderCount { get; set; }
    public int PendingTaskCount { get; set; }
}

public class CompanyWorkspaceDashboardDto
{
    public string CompanyName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalProducts { get; set; }
    public int OpenOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int ActiveEmployees { get; set; }
    public int PendingTasks { get; set; }
    public IReadOnlyList<DashboardSeriesPointDto> DailyOrderLoad { get; set; } = [];
    public IReadOnlyList<DashboardSeriesPointDto> WeeklySalesTrend { get; set; } = [];
    public IReadOnlyList<DashboardSeriesPointDto> MonthlySalesTrend { get; set; } = [];
    public IReadOnlyList<DashboardSeriesPointDto> YearlySalesTrend { get; set; } = [];
    public IReadOnlyList<PerformanceBreakdownDto> ProductPerformance { get; set; } = [];
    public IReadOnlyList<PerformanceBreakdownDto> EmployeePerformance { get; set; } = [];
    public IReadOnlyList<PerformanceBreakdownDto> BusyOrderHours { get; set; } = [];
    public IReadOnlyList<TaskCardDto> Tasks { get; set; } = [];
}

public class EmployeeWorkspaceDto
{
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int AssignedTaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int OpenOrderCount { get; set; }
    public IReadOnlyList<TaskCardDto> Tasks { get; set; } = [];
}

public class PerformanceBreakdownDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class TaskCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; }
    public TaskState Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<TaskHistoryDto> History { get; set; } = [];
}

public class TaskHistoryDto
{
    public string ActionTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public TaskState? PreviousStatus { get; set; }
    public TaskState NewStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCompanyDto
{
    [Required]
    [MaxLength(140)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Code { get; set; }

    [MaxLength(160)]
    public string? Address { get; set; }

    [EmailAddress]
    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(30)]
    public string? ContactPhone { get; set; }
}

public class CreateInternalUserDto
{
    [Required]
    public int CompanyId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Manager|Employee)$")]
    public string Role { get; set; } = string.Empty;
}

public class CreateTaskDto
{
    [Required]
    [MaxLength(140)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public int AssignedToUserId { get; set; }

    [Required]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? DueDate { get; set; }
}

public class UpdateTaskStatusDto
{
    [Required]
    public TaskState Status { get; set; }

    [MaxLength(600)]
    public string? Note { get; set; }
}
