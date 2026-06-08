using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Controllers;

[Authorize(Roles = RoleConstants.OperationalTaskRoles)]
[Route("workspace")]
public class WorkforceController(
    IWorkforceService workforceService,
    ICurrentUserService currentUserService) : Controller
{
    private string OperationsPath => currentUserService.IsInRole(RoleConstants.SystemAdmin) ? "/admin/operations" : "/owner/operations";

    [HttpGet("")]
    public IActionResult Index()
        => currentUserService.IsInRole(RoleConstants.Employee)
            ? RedirectToAction(nameof(MyTasks))
            : Redirect(OperationsPath);

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpGet("/admin/operations")]
    [HttpGet("/owner/operations")]
    [HttpGet("manager")]
    public async Task<IActionResult> ManagerDashboard(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Firma Operasyon Merkezi";
        ViewData["PageDescription"] = "Sipariş yoğunluğu, performans ve görev dagilimi firma bazinda izlenir.";
        return View("ManagerDashboard", await workforceService.GetCompanyDashboardAsync(cancellationToken));
    }

    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Employee}")]
    [HttpGet("/employee")]
    [HttpGet("tasks")]
    public async Task<IActionResult> MyTasks(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Çalışma Alanı";
        ViewData["PageDescription"] = "Görevleriniz, sipariş akışınız ve durum güncellemeleriniz burada yer alir.";
        return View("EmployeeWorkspace", await workforceService.GetEmployeeWorkspaceAsync(cancellationToken));
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost("/admin/tasks")]
    [HttpPost("/owner/tasks")]
    [HttpPost("tasks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTask(CreateTaskDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Görev bilgilerini kontrol edin.";
            return Redirect(OperationsPath);
        }

        await workforceService.AssignTaskAsync(dto, cancellationToken);
        TempData["Success"] = "Görev atandi.";
        return Redirect(OperationsPath);
    }

    [Authorize(Roles = $"{RoleConstants.Manager},{RoleConstants.Employee}")]
    [HttpPost("/employee/tasks/{taskId:int}/status")]
    [HttpPost("tasks/{taskId:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaskStatus(int taskId, UpdateTaskStatusDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Durum bilgilerini kontrol edin.";
            return RedirectToAction(nameof(MyTasks));
        }

        await workforceService.UpdateTaskStatusAsync(taskId, dto, cancellationToken);
        TempData["Success"] = "Görev durumu güncellendi.";
        return RedirectToAction(nameof(MyTasks));
    }
}
