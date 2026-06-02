using System.ComponentModel.DataAnnotations;

namespace MGold.Domain.Entities;

public class Customer
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(30)]
    [RegularExpression(@"^(\+90|90|0)?5\d{9}$", ErrorMessage = "Only Turkish mobile phone numbers are supported.")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(150)]
    public string? Email { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
    public ICollection<CustomerFavorite> Favorites { get; set; } = new List<CustomerFavorite>();
}
