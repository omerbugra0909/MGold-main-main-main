using System.ComponentModel.DataAnnotations;

namespace MGold.Application.DTOs;

public class SubmitContactMessageDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [RegularExpression(@"^(\+90|90|0)?5\d{9}$", ErrorMessage = "Only Turkish mobile phone numbers are supported.")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(2500)]
    public string Message { get; set; } = string.Empty;
}

public class ResolveContactMessageDto
{
    [MaxLength(500)]
    public string? ResolutionNote { get; set; }
}

public class ContactMessageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
