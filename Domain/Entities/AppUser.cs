using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class AppUser
{
    public int Id { get; set; }

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
    [Phone]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Role { get; set; } = string.Empty;

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public ICollection<AppUser> ManagedUsers { get; set; } = new List<AppUser>();
    public ICollection<WorkTask> AssignedTasks { get; set; } = new List<WorkTask>();
    public ICollection<WorkTask> CreatedTasks { get; set; } = new List<WorkTask>();
    public ICollection<WorkTaskHistoryEntry> TaskHistoryEntries { get; set; } = new List<WorkTaskHistoryEntry>();

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public bool PhoneConfirmed { get; set; }
    public DateTime? PhoneConfirmedAt { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndAt { get; set; }
    [MaxLength(80)]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(40)]
    public string ThemePreference { get; set; } = "gold-premium";
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<AccountVerificationToken> VerificationTokens { get; set; } = new List<AccountVerificationToken>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
