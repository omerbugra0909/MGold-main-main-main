using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SecurityStampSnapshot { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? DeviceName { get; set; }

    [MaxLength(80)]
    public string? CreatedByIp { get; set; }

    [MaxLength(300)]
    public string? CreatedByUserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [MaxLength(80)]
    public string? RevokedByIp { get; set; }

    [MaxLength(128)]
    public string? ReplacedByTokenHash { get; set; }

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    public bool IsActive => !IsRevoked && !IsExpired;
}
