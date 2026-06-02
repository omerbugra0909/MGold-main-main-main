using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Common;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class ContactService(
    AppDbContext context,
    IEmailService emailService,
    ISmsService smsService,
    IOptions<EmailSettings> emailOptions,
    IOptions<SmsSettings> smsOptions,
    IAccessControlService accessControlService) : IContactService
{
    private readonly EmailSettings _emailSettings = emailOptions.Value;
    private readonly SmsSettings _smsSettings = smsOptions.Value;

    public async Task<ContactMessageDto> SubmitAsync(SubmitContactMessageDto dto, CancellationToken cancellationToken = default)
    {
        var name = NormalizeRequired(dto.Name, "Name");
        var phone = NormalizePhone(dto.Phone);
        var email = NormalizeRequired(dto.Email, "Email");
        var subject = NormalizeRequired(dto.Subject, "Subject");
        var message = NormalizeRequired(dto.Message, "Message");

        var entity = new Domain.Entities.ContactMessage
        {
            Name = name,
            Phone = phone,
            Email = email,
            Subject = subject,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };

        await context.ContactMessages.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(_emailSettings.SupportInbox))
        {
            await emailService.SendAsync(new SendEmailRequestDto
            {
                To = _emailSettings.SupportInbox,
                Subject = $"[MGold Iletisim] {entity.Subject}",
                HtmlBody = $"<p><strong>Ad:</strong> {WebUtility.HtmlEncode(entity.Name)}</p><p><strong>Telefon:</strong> {WebUtility.HtmlEncode(entity.Phone)}</p><p><strong>E-posta:</strong> {WebUtility.HtmlEncode(entity.Email)}</p><p><strong>Mesaj:</strong><br />{WebUtility.HtmlEncode(entity.Message).Replace(Environment.NewLine, "<br />")}</p>"
            }, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_smsSettings.SupportPhone))
        {
            await smsService.SendAsync(new SendSmsRequestDto
            {
                ToPhone = _smsSettings.SupportPhone,
                Message = $"Yeni iletisim mesaji: {entity.Name} - {entity.Subject}"
            }, cancellationToken);
        }

        await smsService.SendAsync(new SendSmsRequestDto
        {
            ToPhone = entity.Phone,
            Message = $"MGold iletisim talebiniz alindi. Konu: {entity.Subject}"
        }, cancellationToken);

        return entity.ToDto();
    }

    public async Task<IReadOnlyList<ContactMessageDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();
        var items = await context.ContactMessages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<ContactMessageDto> ResolveAsync(int id, ResolveContactMessageDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();

        var entity = await context.ContactMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Contact message with id {id} was not found.");

        entity.IsResolved = true;
        entity.ResolvedAt = DateTime.UtcNow;
        entity.ResolutionNote = string.IsNullOrWhiteSpace(dto.ResolutionNote) ? null : dto.ResolutionNote.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException($"{fieldName} is required.");
        }

        return normalized;
    }

    private static string NormalizePhone(string? phone)
    {
        if (!TurkishPhoneHelper.TryNormalize(phone, out var normalized))
        {
            throw new BusinessRuleException("Only Turkish mobile phone numbers are supported. Use a 5xx number.");
        }

        return normalized;
    }
}
