using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class AccountVerificationService(
    AppDbContext context,
    IEmailService emailService,
    ISmsService smsService,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<EmailSettings> emailOptions,
    ILogger<AccountVerificationService> logger) : IAccountVerificationService
{
    private readonly EmailSettings emailSettings = emailOptions.Value;
    private const string PurposeEmailConfirmation = "email_confirmation";
    private const string PurposePhoneConfirmation = "phone_confirmation";
    private const string PurposePasswordReset = "password_reset";
    private const int MaxActiveTokensPerPurpose = 3;
    private const int MaxAttempts = 5;
    private const int MaxRequestsPerIpPerHour = 10;
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EmailConfirmationLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan PhoneConfirmationLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(15);

    public async Task SendEmailConfirmationAsync(AppUser user, string baseUrl, string? requestIp, CancellationToken cancellationToken = default)
    {
        EnsureEmailDeliveryConfigured();
        var token = GenerateSecureToken();
        await CreateTokenAsync(user, PurposeEmailConfirmation, "email", user.Email, token, EmailConfirmationLifetime, requestIp, cancellationToken);

        var verifyUrl = $"{baseUrl.TrimEnd('/')}/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await emailService.SendAsync(new SendEmailRequestDto
        {
            To = user.Email,
            Subject = "MGold e-posta doğrulama",
            HtmlBody = $"""
                <h2>MGold hesabıni dogrula</h2>
                <p>Merhaba {WebUtilityHtmlEncode(user.FullName)}, hesabıni aktif kullanabilmek için e-posta adresini doğrulaman gerekiyor.</p>
                <p><a href="{verifyUrl}" style="display:inline-block;padding:12px 18px;background:#111827;color:#f8e7b0;text-decoration:none;border-radius:10px;">E-postami Dogrula</a></p>
                <p>Bu bağlantı 24 saat geçerlidir. Bu işlemi sen başlatmadıysan bu e-postayi yok sayabilirsin.</p>
            """
        }, cancellationToken);
    }

    public async Task SendEmailConfirmationAsync(string identifier, string baseUrl, string? requestIp, CancellationToken cancellationToken = default)
    {
        var lookup = identifier.Trim().ToLowerInvariant();
        var user = await context.AppUsers.FirstOrDefaultAsync(x => x.Email == lookup || x.Username == lookup, cancellationToken);
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        await SendEmailConfirmationAsync(user, baseUrl, requestIp, cancellationToken);
    }

    public async Task SendPhoneConfirmationAsync(string identifier, string? requestIp, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(identifier, cancellationToken);
        if (user is null || user.PhoneConfirmed)
        {
            return;
        }

        var code = GenerateNumericCode();
        await CreateTokenAsync(user, PurposePhoneConfirmation, "sms", user.Phone, code, PhoneConfirmationLifetime, requestIp, cancellationToken);
        await smsService.SendAsync(new SendSmsRequestDto
        {
            ToPhone = user.Phone,
            Message = $"MGold telefon doğrulama kodunuz: {code}. Kod {PhoneConfirmationLifetime.TotalMinutes:N0} dakika geçerlidir."
        }, cancellationToken);
    }

    public async Task<EmailVerificationResultDto> ConfirmPhoneAsync(VerifyPhoneRequestDto dto, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(dto.Identifier, cancellationToken);
        if (user is null)
        {
            return new EmailVerificationResultDto { Success = false, Message = "Telefon doğrulama isteği dogrulanamadi." };
        }

        var verification = await ValidateTokenAsync(user, PurposePhoneConfirmation, "sms", dto.Code, cancellationToken);
        if (!verification.Success)
        {
            return verification;
        }

        user.PhoneConfirmed = true;
        user.PhoneConfirmedAt = DateTime.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);

        return new EmailVerificationResultDto { Success = true, Message = "Telefon numaraniz dogrulandi." };
    }

    public async Task<EmailVerificationResultDto> ConfirmEmailAsync(int userId, string token, CancellationToken cancellationToken = default)
    {
        var user = await context.AppUsers.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return new EmailVerificationResultDto { Success = false, Message = "Doğrulama kaydı bulunamadı." };
        }

        var verification = await ValidateTokenAsync(user, PurposeEmailConfirmation, "email", token, cancellationToken);
        if (!verification.Success)
        {
            return verification;
        }

        user.EmailConfirmed = true;
        user.EmailConfirmedAt = DateTime.UtcNow;
        user.IsActive = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync(cancellationToken);

        return new EmailVerificationResultDto { Success = true, Message = "E-posta adresiniz dogrulandi. Artik giriş yapabilirsiniz." };
    }

    public async Task StartPasswordResetAsync(ForgotPasswordRequestDto dto, string baseUrl, string? requestIp, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(dto.Identifier, cancellationToken);
        if (user is null || !user.IsActive || !user.EmailConfirmed)
        {
            // Do not reveal whether the account exists.
            logger.LogInformation("Password reset requested for unavailable account: {Identifier}", dto.Identifier);
            return;
        }

        if (string.Equals(dto.Channel, "sms", StringComparison.OrdinalIgnoreCase))
        {
            var code = GenerateNumericCode();
            await CreateTokenAsync(user, PurposePasswordReset, "sms", user.Phone, code, PasswordResetLifetime, requestIp, cancellationToken);
            await smsService.SendAsync(new SendSmsRequestDto
            {
                ToPhone = user.Phone,
                Message = $"MGold şifre yenileme kodunuz: {code}. Kod {PasswordResetLifetime.TotalMinutes:N0} dakika geçerlidir."
            }, cancellationToken);
            return;
        }

        EnsureEmailDeliveryConfigured();
        var token = GenerateSecureToken();
        await CreateTokenAsync(user, PurposePasswordReset, "email", user.Email, token, PasswordResetLifetime, requestIp, cancellationToken);
        var resetUrl = $"{baseUrl.TrimEnd('/')}/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}&channel=email";
        await emailService.SendAsync(new SendEmailRequestDto
        {
            To = user.Email,
            Subject = "MGold şifre yenileme",
            HtmlBody = $"""
                <h2>Şifre yenileme isteği</h2>
                <p>Merhaba {WebUtilityHtmlEncode(user.FullName)}, şifreni yenilemek için aşağıdaki bağlantıyi kullanabilirsin.</p>
                <p><a href="{resetUrl}" style="display:inline-block;padding:12px 18px;background:#111827;color:#f8e7b0;text-decoration:none;border-radius:10px;">Şifremi Yenile</a></p>
                <p>Bu bağlantı 15 dakika geçerlidir. Bu isteği sen başlatmadıysan hesabın için herhangi bir işlem yapma.</p>
            """
        }, cancellationToken);
    }

    public async Task<EmailVerificationResultDto> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        ValidatePasswordStrength(dto.NewPassword);

        var user = dto.UserId.HasValue
            ? await context.AppUsers.FirstOrDefaultAsync(x => x.Id == dto.UserId.Value, cancellationToken)
            : await FindUserByIdentifierAsync(dto.Identifier ?? string.Empty, cancellationToken);

        if (user is null || !user.IsActive || !user.EmailConfirmed)
        {
            return new EmailVerificationResultDto { Success = false, Message = "Şifre yenileme isteği dogrulanamadi." };
        }

        var channel = string.Equals(dto.Channel, "sms", StringComparison.OrdinalIgnoreCase) ? "sms" : "email";
        var verification = await ValidateTokenAsync(user, PurposePasswordReset, channel, dto.TokenOrCode, cancellationToken);
        if (!verification.Success)
        {
            return verification;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEndAt = null;
        await context.SaveChangesAsync(cancellationToken);

        return new EmailVerificationResultDto { Success = true, Message = "Şifreniz yenilendi. Yeni şifrenizle giriş yapabilirsiniz." };
    }

    private async Task<AppUser?> FindUserByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var lookup = identifier.Trim().ToLowerInvariant();
        var hasPhone = TurkishPhoneHelper.TryNormalize(lookup, out var normalizedPhone);
        return await context.AppUsers.FirstOrDefaultAsync(
            x => x.Email == lookup || x.Username == lookup || (hasPhone && x.Phone == normalizedPhone),
            cancellationToken);
    }

    private async Task CreateTokenAsync(
        AppUser user,
        string purpose,
        string channel,
        string destination,
        string token,
        TimeSpan lifetime,
        string? requestIp,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(requestIp))
        {
            var ipWindowStart = now.AddHours(-1);
            var requestCount = await context.AccountVerificationTokens
                .CountAsync(x => x.RequestIp == requestIp
                    && x.Purpose == purpose
                    && x.CreatedAt >= ipWindowStart,
                    cancellationToken);
            if (requestCount >= MaxRequestsPerIpPerHour)
            {
                throw new BusinessRuleException("Çok fazla doğrulama isteği alındı. Lutfen daha sonra tekrar deneyin.");
            }
        }

        var recentActiveTokens = await context.AccountVerificationTokens
            .Where(x => x.AppUserId == user.Id
                && x.Purpose == purpose
                && x.Channel == channel
                && x.ConsumedAt == null
                && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (recentActiveTokens.FirstOrDefault() is { } lastToken
            && now - lastToken.LastSentAt < ResendCooldown)
        {
            throw new BusinessRuleException("Yeni kod/baglanti göndermeden önce kısa bir sure bekleyin.");
        }

        if (recentActiveTokens.Count >= MaxActiveTokensPerPurpose)
        {
            foreach (var oldToken in recentActiveTokens.Skip(MaxActiveTokensPerPurpose - 1))
            {
                oldToken.ConsumedAt = now;
            }
        }

        context.AccountVerificationTokens.Add(new AccountVerificationToken
        {
            AppUserId = user.Id,
            Purpose = purpose,
            Channel = channel,
            Destination = destination,
            TokenHash = HashToken(token),
            ExpiresAt = now.Add(lifetime),
            CreatedAt = now,
            LastSentAt = now,
            RequestIp = requestIp
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<EmailVerificationResultDto> ValidateTokenAsync(
        AppUser user,
        string purpose,
        string channel,
        string token,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hash = HashToken(token.Trim());
        var verification = await context.AccountVerificationTokens
            .Where(x => x.AppUserId == user.Id
                && x.Purpose == purpose
                && x.Channel == channel
                && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification is null)
        {
            return new EmailVerificationResultDto { Success = false, Message = "Doğrulama kodu veya bağlantısi bulunamadı." };
        }

        if (verification.ExpiresAt <= now)
        {
            verification.ConsumedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return new EmailVerificationResultDto { Success = false, Message = "Doğrulama suresi doldu. Yeni kod isteyin." };
        }

        if (verification.Attempts >= MaxAttempts)
        {
            verification.ConsumedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return new EmailVerificationResultDto { Success = false, Message = "Çok fazla hatalı deneme yapildi. Yeni kod isteyin." };
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(verification.TokenHash), Encoding.UTF8.GetBytes(hash)))
        {
            verification.Attempts++;
            await context.SaveChangesAsync(cancellationToken);
            return new EmailVerificationResultDto { Success = false, Message = "Doğrulama kodu hatalı." };
        }

        verification.ConsumedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return new EmailVerificationResultDto { Success = true, Message = "Doğrulama basarili." };
    }

    private static string GenerateSecureToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');

    private static string GenerateNumericCode()
        => RandomNumberGenerator.GetInt32(100000, 999999).ToString();

    private static void ValidatePasswordStrength(string password)
    {
        if (password.Length < 8
            || !password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit))
        {
            throw new BusinessRuleException("Şifre en az 8 karakter olmalı ve büyük harf, küçük harf ve rakam içermelidir.");
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string WebUtilityHtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);

    private void EnsureEmailDeliveryConfigured()
    {
        if (!emailSettings.Enabled)
        {
            throw new BusinessRuleException("E-posta servisi kapali. Lutfen Email:Enabled ayarini true yapın.");
        }

        if (string.IsNullOrWhiteSpace(emailSettings.Host)
            || emailSettings.Port <= 0
            || string.IsNullOrWhiteSpace(emailSettings.Username)
            || string.IsNullOrWhiteSpace(ResolveEmailPassword()))
        {
            throw new BusinessRuleException("E-posta servisi eksik yapilandirilmis. Gmail app şifresi ve SMTP ayarlarini kontrol edin.");
        }
    }

    private string ResolveEmailPassword()
    {
        var configured = emailSettings.Password;
        if (string.IsNullOrWhiteSpace(configured)
            || configured.Contains("BURAYA", StringComparison.OrdinalIgnoreCase)
            || configured.Contains("GOOGLE_APP_PASSWORD", StringComparison.OrdinalIgnoreCase))
        {
            configured = Environment.GetEnvironmentVariable("MGOLD_EMAIL_PASSWORD")
                ?? Environment.GetEnvironmentVariable("EMAIL_PASSWORD")
                ?? string.Empty;
        }

        return emailSettings.Host.Contains("gmail", StringComparison.OrdinalIgnoreCase)
            ? configured.Replace(" ", string.Empty, StringComparison.Ordinal)
            : configured;
    }
}
