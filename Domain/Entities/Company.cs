using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class Company
{
    public int Id { get; set; }

    [Required]
    [MaxLength(140)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Code { get; set; }

    [MaxLength(160)]
    public string? Address { get; set; }

    [MaxLength(150)]
    public string? ContactEmail { get; set; }

    [MaxLength(30)]
    public string? ContactPhone { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
}
