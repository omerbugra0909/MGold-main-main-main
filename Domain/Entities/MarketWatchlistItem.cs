using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class MarketWatchlistItem
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;

    [Required]
    [MaxLength(40)]
    public string Symbol { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
