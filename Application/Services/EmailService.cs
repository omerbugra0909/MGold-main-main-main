using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;

namespace MGold.Application.Services;

public class EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendAsync(SendEmailRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("Email sending skipped because Email:Enabled is false. Subject: {Subject}", dto.Subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = dto.Subject,
            Body = dto.HtmlBody,
            IsBodyHtml = true
        };
        message.To.Add(dto.To);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }
}
