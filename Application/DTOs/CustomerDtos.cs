using System.ComponentModel.DataAnnotations;

namespace MGold.Application.DTOs;

public class CreateCustomerDto
{
    [Required]
    [MaxLength(120)]
    [RegularExpression(@".*\S.*", ErrorMessage = "Customer name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(30)]
    [RegularExpression(@"^(\+90|90|0)?5\d{9}$", ErrorMessage = "Only Turkish mobile phone numbers are supported.")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(150)]
    public string? Email { get; set; }
}

public class UpdateCustomerDto : CreateCustomerDto
{
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}
