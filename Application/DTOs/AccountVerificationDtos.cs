using System.ComponentModel.DataAnnotations;

namespace MGold.Application.DTOs;

public class EmailVerificationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ForgotPasswordRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(email|sms)$")]
    public string Channel { get; set; } = "email";
}

public class ResetPasswordRequestDto
{
    public int? UserId { get; set; }

    [MaxLength(150)]
    public string? Identifier { get; set; }

    [Required]
    [MaxLength(160)]
    public string TokenOrCode { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(64)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(email|sms)$")]
    public string Channel { get; set; } = "email";
}

public class VerifyPhoneRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Code { get; set; } = string.Empty;
}
