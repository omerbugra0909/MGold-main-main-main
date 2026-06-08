using System.Net;
using System.Net.Mail;
using System.Text;
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

        ValidateSettings();

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = dto.Subject,
            Body = dto.HtmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(dto.To);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_settings.Username, ResolvePassword()),
            Timeout = 20000
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Email sent to {Recipient}. Subject: {Subject}", dto.To, dto.Subject);
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            throw new InvalidOperationException("Email:Host is not configured.");
        }

        if (_settings.Port <= 0)
        {
            throw new InvalidOperationException("Email:Port is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.Username))
        {
            throw new InvalidOperationException("Email:Username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(ResolvePassword()))
        {
            throw new InvalidOperationException("Email:Password is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_settings.FromEmail))
        {
            _settings.FromEmail = _settings.Username;
        }
    }

    private string ResolvePassword()
    {
        var configured = _settings.Password;
        if (string.IsNullOrWhiteSpace(configured)
            || configured.Contains("BURAYA", StringComparison.OrdinalIgnoreCase)
            || configured.Contains("GOOGLE_APP_PASSWORD", StringComparison.OrdinalIgnoreCase))
        {
            configured = Environment.GetEnvironmentVariable("MGOLD_EMAIL_PASSWORD")
                ?? Environment.GetEnvironmentVariable("EMAIL_PASSWORD")
                ?? string.Empty;
        }

        if (_settings.Host.Contains("gmail", StringComparison.OrdinalIgnoreCase))
        {
            configured = configured.Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        return configured;
    }
}
