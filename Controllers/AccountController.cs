using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class AccountController(
    IAuthService authService,
    IAccountVerificationService accountVerificationService,
    IConfiguration configuration) : Controller
{
    [AllowAnonymous]
    [HttpGet("/auth")]
    public IActionResult Index()
        => View("Portal");

    [AllowAnonymous]
    [HttpGet("/auth/login")]
    public IActionResult UserLogin(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new UserLoginViewModel());
    }

    [AllowAnonymous]
    [HttpGet("/auth/register")]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new UserRegisterViewModel());
    }

    [AllowAnonymous]
    [HttpGet("/admin/login")]
    public IActionResult AdminLogin(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new AdminLoginViewModel());
    }

    [AllowAnonymous]
    [HttpGet("/workspace/login")]
    public IActionResult WorkspaceLogin(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("AdminLogin", new AdminLoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost("/auth/login")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UserLogin(UserLoginViewModel form, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(form);
        }

        try
        {
            var auth = await authService.LoginCustomerAsync(new LoginRequestDto
            {
                EmailOrUsername = form.EmailOrUsername,
                Password = form.Password
            }, cancellationToken);

            await SignInAsync(auth);
            TempData["Success"] = $"Hos geldiniz, {auth.FullName}.";
            return Redirect(auth.RedirectUrl);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["ReturnUrl"] = returnUrl;
            return View(form);
        }
    }

    [AllowAnonymous]
    [HttpPost("/auth/register")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(UserRegisterViewModel form, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(form);
        }

        try
        {
            var auth = await authService.RegisterAsync(new RegisterRequestDto
            {
                Username = form.Username,
                FullName = form.FullName,
                Email = form.Email,
                Phone = form.Phone,
                Password = form.Password,
                Role = RoleConstants.Customer
            }, cancellationToken);

            try
            {
                await accountVerificationService.SendEmailConfirmationAsync(auth.Email, GetBaseUrl(), GetRequestIp(), cancellationToken);
                TempData["Success"] = "Hesabiniz oluşturuldu. Gönderdigimiz e-posta doğrulama bağlantısini açarak hesabınızı aktiflestirin.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hesabiniz oluşturuldu fakat doğrulama e-postasi gönderilemedi: {ex.Message}";
            }

            return RedirectToAction(nameof(VerifyPending), new { email = auth.Email });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["ReturnUrl"] = returnUrl;
            return View(form);
        }
    }

    [AllowAnonymous]
    [HttpPost("/admin/login")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogin(AdminLoginViewModel form, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(form);
        }

        try
        {
            var auth = await authService.LoginAdminAsync(new LoginRequestDto
            {
                EmailOrUsername = form.Username,
                Password = form.Password,
                RequireAdminPortal = true
            }, cancellationToken);

            await SignInAsync(auth);
            TempData["Success"] = "Yönetim paneli oturumu acildi.";
            return Redirect(auth.RedirectUrl);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["ReturnUrl"] = returnUrl;
            return View(form);
        }
    }

    [AllowAnonymous]
    [HttpPost("/workspace/login")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkspaceLogin(AdminLoginViewModel form, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View("AdminLogin", form);
        }

        try
        {
            var auth = await authService.LoginWorkspaceAsync(new LoginRequestDto
            {
                EmailOrUsername = form.Username,
                Password = form.Password
            }, cancellationToken);

            await SignInAsync(auth);
            TempData["Success"] = "Çalışma alanı oturumu acildi.";
            return Redirect(auth.RedirectUrl);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["ReturnUrl"] = returnUrl;
            return View("AdminLogin", form);
        }
    }

    [Authorize(Roles = RoleConstants.AuthenticatedPortalRoles)]
    [HttpPost("/account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Cikis yapildi.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    [HttpGet("/auth/verify-pending")]
    public IActionResult VerifyPending(string? email = null)
        => View(new VerifyPendingViewModel { Email = email ?? string.Empty, EmailOrUsername = email ?? string.Empty });

    [AllowAnonymous]
    [HttpPost("/auth/resend-verification")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerification(VerifyPendingViewModel form, CancellationToken cancellationToken)
    {
        try
        {
            await accountVerificationService.SendEmailConfirmationAsync(form.EmailOrUsername, GetBaseUrl(), GetRequestIp(), cancellationToken);
            TempData["Success"] = "Doğrulama e-postasi tekrar gönderildi.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(VerifyPending), new { email = form.EmailOrUsername });
    }

    [AllowAnonymous]
    [HttpPost("/auth/resend-phone-verification")]
    [EnableRateLimiting("sms")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendPhoneVerification(VerifyPendingViewModel form, CancellationToken cancellationToken)
    {
        try
        {
            await accountVerificationService.SendPhoneConfirmationAsync(form.EmailOrUsername, GetRequestIp(), cancellationToken);
            TempData["Success"] = "Telefon doğrulama kodu gönderildi.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(VerifyPending), new { email = form.EmailOrUsername });
    }

    [AllowAnonymous]
    [HttpPost("/auth/verify-phone")]
    [EnableRateLimiting("sms")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPhone(VerifyPendingViewModel form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(form.PhoneCode))
        {
            TempData["Error"] = "Telefon doğrulama kodu zorunludur.";
            return RedirectToAction(nameof(VerifyPending), new { email = form.EmailOrUsername });
        }

        var result = await accountVerificationService.ConfirmPhoneAsync(new VerifyPhoneRequestDto
        {
            Identifier = form.EmailOrUsername,
            Code = form.PhoneCode
        }, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(VerifyPending), new { email = form.EmailOrUsername });
    }

    [AllowAnonymous]
    [HttpGet("/auth/verify-email")]
    public async Task<IActionResult> VerifyEmail(int userId, string token, CancellationToken cancellationToken)
    {
        var result = await accountVerificationService.ConfirmEmailAsync(userId, token, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(result.Success ? nameof(UserLogin) : nameof(VerifyPending));
    }

    [AllowAnonymous]
    [HttpGet("/auth/forgot-password")]
    public IActionResult ForgotPassword()
        => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost("/auth/forgot-password")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            await accountVerificationService.StartPasswordResetAsync(new ForgotPasswordRequestDto
            {
                Identifier = form.Identifier,
                Channel = form.Channel
            }, GetBaseUrl(), GetRequestIp(), cancellationToken);

            TempData["Success"] = "Bilgiler eslesiyorsa şifre yenileme kodu/baglantisi gönderildi.";
            return RedirectToAction(nameof(ResetPassword), new { identifier = form.Identifier, channel = form.Channel });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
    }

    [AllowAnonymous]
    [HttpGet("/auth/reset-password")]
    public IActionResult ResetPassword(int? userId = null, string? token = null, string? channel = null, string? identifier = null)
        => View(new ResetPasswordViewModel
        {
            UserId = userId,
            Identifier = identifier ?? string.Empty,
            TokenOrCode = token ?? string.Empty,
            Channel = string.IsNullOrWhiteSpace(channel) ? "email" : channel
        });

    [AllowAnonymous]
    [HttpPost("/auth/reset-password")]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await accountVerificationService.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            UserId = form.UserId,
            Identifier = form.Identifier,
            TokenOrCode = form.TokenOrCode,
            NewPassword = form.NewPassword,
            Channel = form.Channel
        }, cancellationToken);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return result.Success ? RedirectToAction(nameof(UserLogin)) : View(form);
    }

    private async Task SignInAsync(AuthResponseDto auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId.ToString()),
            new(ClaimTypes.Name, auth.Username),
            new(ClaimTypes.GivenName, auth.FullName),
            new(ClaimTypes.Email, auth.Email),
            new(ClaimTypes.MobilePhone, auth.Phone),
            new(ClaimTypes.Role, auth.SystemRole),
            new("client_role", auth.Role),
            new("theme", auth.ThemePreference)
        };

        if (auth.CompanyId.HasValue)
        {
            claims.Add(new Claim("company_id", auth.CompanyId.Value.ToString()));
            claims.Add(new Claim("firm_id", auth.CompanyId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });
    }

    private string GetBaseUrl()
    {
        var configuredBaseUrl = configuration["App:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? Request.Scheme : forwardedProto;
        return $"{scheme}://{Request.Host}";
    }

    private string? GetRequestIp()
        => HttpContext.Connection.RemoteIpAddress?.ToString();

}

public class UserLoginViewModel
{
    [Required(ErrorMessage = "E-posta veya kullanıcı adi zorunludur.")]
    [MaxLength(150)]
    public string EmailOrUsername { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;
}

public class UserRegisterViewModel
{
    [Required(ErrorMessage = "Kullanıcı adi zorunludur.")]
    [MaxLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon numarasi zorunludur.")]
    [RegularExpression(@"^(\+90|90|0)?5\d{9}$", ErrorMessage = "Geçerli bir telefon numarasi giriniz.")]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdir.")]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;
}

public class AdminLoginViewModel
{
    [Required(ErrorMessage = "Admin kullanıcı adi zorunludur.")]
    [MaxLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;
}

public class VerifyPendingViewModel
{
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta veya kullanıcı adi zorunludur.")]
    [MaxLength(150)]
    public string EmailOrUsername { get; set; } = string.Empty;

    [MaxLength(16)]
    public string? PhoneCode { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta, telefon veya kullanıcı adi zorunludur.")]
    [MaxLength(150)]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(email|sms)$")]
    public string Channel { get; set; } = "email";
}

public class ResetPasswordViewModel
{
    public int? UserId { get; set; }

    [MaxLength(150)]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod veya token zorunludur.")]
    [MaxLength(160)]
    public string TokenOrCode { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(email|sms)$")]
    public string Channel { get; set; } = "email";

    [Required(ErrorMessage = "Yeni şifre zorunludur.")]
    [MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdir.")]
    [MaxLength(64)]
    public string NewPassword { get; set; } = string.Empty;
}
