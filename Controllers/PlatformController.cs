using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Controllers;

[Authorize(Roles = RoleConstants.SystemAdminOnly)]
[Route("platform")]
[Route("admin/platform")]
public class PlatformController(IWorkforceService workforceService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Platform Yönetimi";
        ViewData["PageDescription"] = "Firmalar, yöneticiler ve platform organizasyonu tek merkezden yönetilir.";
        return View(await workforceService.GetPlatformDashboardAsync(cancellationToken));
    }

    [HttpPost("companies")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompany(CreateCompanyDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Firma bilgilerini kontrol edin.";
            return Redirect("/admin/platform");
        }

        await workforceService.CreateCompanyAsync(dto, cancellationToken);
        TempData["Success"] = "Yeni firma oluşturuldu.";
        return Redirect("/admin/platform");
    }

    [HttpPost("users")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManager(CreateInternalUserDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Kullanıcı bilgilerini kontrol edin.";
            return Redirect("/admin/platform");
        }

        await workforceService.CreateInternalUserAsync(dto, cancellationToken);
        TempData["Success"] = "Firma kullanıcısi oluşturuldu.";
        return Redirect("/admin/platform");
    }
}
