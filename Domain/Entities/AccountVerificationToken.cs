using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class AccountVerificationToken
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    [Required]
    [MaxLength(40)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Channel { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public int Attempts { get; set; }
    public int SendCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSentAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    [MaxLength(80)]
    public string? RequestIp { get; set; }
}
