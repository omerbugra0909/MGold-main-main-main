using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class MarketProviderConfiguration
{
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string ProviderKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
    public bool SupportsRealtime { get; set; } = true;
    public int Priority { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 30;

    [MaxLength(200)]
    public string? BaseUrl { get; set; }

    [MaxLength(200)]
    public string? ApiKey { get; set; }

    public DateTime? LastSuccessfulSyncAt { get; set; }
    public DateTime? LastFailureAt { get; set; }

    [MaxLength(400)]
    public string? LastError { get; set; }

    public int FailureCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
