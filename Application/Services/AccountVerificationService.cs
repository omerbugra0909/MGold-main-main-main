using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    ILogger<AccountVerificationService> logger) : IAccountVerificationService
{
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
        var token = GenerateSecureToken();
        await CreateTokenAsync(user, PurposeEmailConfirmation, "email", user.Email, token, EmailConfirmationLifetime, requestIp, cancellationToken);

        var verifyUrl = $"{baseUrl.TrimEnd('/')}/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await emailService.SendAsync(new SendEmailRequestDto
        {
            To = user.Email,
            Subject = "MGold e-posta dogrulama",
            HtmlBody = $"""
                <h2>MGold hesabini dogrula</h2>
                <p>Merhaba {WebUtilityHtmlEncode(user.FullName)}, hesabini aktif kullanabilmek icin e-posta adresini dogrulaman gerekiyor.</p>
                <p><a href="{verifyUrl}" style="display:inline-block;padding:12px 18px;background:#111827;color:#f8e7b0;text-decoration:none;border-radius:10px;">E-postami Dogrula</a></p>
                <p>Bu baglanti 24 saat gecerlidir. Bu islemi sen baslatmadiysan bu e-postayi yok sayabilirsin.</p>
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
            Message = $"MGold telefon dogrulama kodunuz: {code}. Kod {PhoneConfirmationLifetime.TotalMinutes:N0} dakika gecerlidir."
        }, cancellationToken);
    }

    public async Task<EmailVerificationResultDto> ConfirmPhoneAsync(VerifyPhoneRequestDto dto, CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdentifierAsync(dto.Identifier, cancellationToken);
        if (user is null)
        {
            return new EmailVerificationResultDto { Success = false, Message = "Telefon dogrulama istegi dogrulanamadi." };
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
            return new EmailVerificationResultDto { Success = false, Message = "Dogrulama kaydi bulunamadi." };
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

        return new EmailVerificationResultDto { Success = true, Message = "E-posta adresiniz dogrulandi. Artik giris yapabilirsiniz." };
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
                Message = $"MGold sifre yenileme kodunuz: {code}. Kod {PasswordResetLifetime.TotalMinutes:N0} dakika gecerlidir."
            }, cancellationToken);
            return;
        }

        var token = GenerateSecureToken();
        await CreateTokenAsync(user, PurposePasswordReset, "email", user.Email, token, PasswordResetLifetime, requestIp, cancellationToken);
        var resetUrl = $"{baseUrl.TrimEnd('/')}/auth/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}&channel=email";
        await emailService.SendAsync(new SendEmailRequestDto
        {
            To = user.Email,
            Subject = "MGold sifre yenileme",
            HtmlBody = $"""
                <h2>Sifre yenileme istegi</h2>
                <p>Merhaba {WebUtilityHtmlEncode(user.FullName)}, sifreni yenilemek icin asagidaki baglantiyi kullanabilirsin.</p>
                <p><a href="{resetUrl}" style="display:inline-block;padding:12px 18px;background:#111827;color:#f8e7b0;text-decoration:none;border-radius:10px;">Sifremi Yenile</a></p>
                <p>Bu baglanti 15 dakika gecerlidir. Bu istegi sen baslatmadiysan hesabin icin herhangi bir islem yapma.</p>
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
            return new EmailVerificationResultDto { Success = false, Message = "Sifre yenileme istegi dogrulanamadi." };
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

        return new EmailVerificationResultDto { Success = true, Message = "Sifreniz yenilendi. Yeni sifrenizle giris yapabilirsiniz." };
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
                throw new BusinessRuleException("Cok fazla dogrulama istegi alindi. Lutfen daha sonra tekrar deneyin.");
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
            throw new BusinessRuleException("Yeni kod/baglanti gondermeden once kisa bir sure bekleyin.");
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
            return new EmailVerificationResultDto { Success = false, Message = "Dogrulama kodu veya baglantisi bulunamadi." };
        }

        if (verification.ExpiresAt <= now)
        {
            verification.ConsumedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return new EmailVerificationResultDto { Success = false, Message = "Dogrulama suresi doldu. Yeni kod isteyin." };
        }

        if (verification.Attempts >= MaxAttempts)
        {
            verification.ConsumedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return new EmailVerificationResultDto { Success = false, Message = "Cok fazla hatali deneme yapildi. Yeni kod isteyin." };
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(verification.TokenHash), Encoding.UTF8.GetBytes(hash)))
        {
            verification.Attempts++;
            await context.SaveChangesAsync(cancellationToken);
            return new EmailVerificationResultDto { Success = false, Message = "Dogrulama kodu hatali." };
        }

        verification.ConsumedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return new EmailVerificationResultDto { Success = true, Message = "Dogrulama basarili." };
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
            throw new BusinessRuleException("Sifre en az 8 karakter olmali ve buyuk harf, kucuk harf ve rakam icermelidir.");
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static string WebUtilityHtmlEncode(string value)
        => System.Net.WebUtility.HtmlEncode(value);
}
