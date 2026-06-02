namespace MGold.Application.DTOs;

public class SmsSettings
{
    public const string SectionName = "Sms";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Netgsm";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Originator { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://api.netgsm.com.tr/sms/send/get/";
    public string? SupportPhone { get; set; }
}

public class SendSmsRequestDto
{
    public string ToPhone { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
