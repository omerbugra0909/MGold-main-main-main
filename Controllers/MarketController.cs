using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;

namespace MGold.Controllers;

public class MarketController(IMarketDataService marketDataService) : Controller
{
    private string AdminMarketPath => User.IsInRole(RoleConstants.SystemAdmin) ? "/admin/market" : "/owner/market";

    [AllowAnonymous]
    [HttpGet("/market")]
    public async Task<IActionResult> Public(string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var model = await BuildPageModelAsync(
            title: "Canlı Piyasalar",
            description: "Altin, döviz, metaller ve emtia akışlarını herkes için açık market panelinde takip edin.",
            isAdmin: false,
            baseCurrency,
            cancellationToken);

        return View("~/Views/Customer/Market.cshtml", model);
    }

    [Authorize(Roles = RoleConstants.CustomerOnly)]
    [HttpGet("/customer/market")]
    public async Task<IActionResult> Customer(string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var model = await BuildPageModelAsync(
            title: "Canlı Piyasalar",
            description: "Altin, döviz, metaller ve emtia akışlarını premium market panelinde takip edin.",
            isAdmin: false,
            baseCurrency,
            cancellationToken);

        return View("~/Views/Customer/Market.cshtml", model);
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpGet("/owner/market")]
    [HttpGet("/admin/market")]
    public async Task<IActionResult> Admin(string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var model = await BuildPageModelAsync(
            title: "Market Control Center",
            description: "Canlı piyasa akışlarını, provider sağlığını ve veri kaynaklarını tek panelden yönetin.",
            isAdmin: true,
            baseCurrency,
            cancellationToken);

        return View("~/Views/Admin/Market.cshtml", model);
    }

    [AllowAnonymous]
    [HttpGet("/api/market/dashboard")]
    public async Task<IActionResult> Dashboard(string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        var data = await marketDataService.GetDashboardAsync(baseCurrency, username, cancellationToken);
        return Json(data);
    }

    [AllowAnonymous]
    [HttpGet("/api/market/quotes/{symbol}")]
    public async Task<IActionResult> QuoteDetail(string symbol, string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        var detail = await marketDataService.GetQuoteDetailAsync(symbol, baseCurrency, username, cancellationToken);
        return detail is null ? NotFound() : Json(detail);
    }

    [Authorize(Roles = RoleConstants.CustomerOnly)]
    [HttpPost("/customer/market/watchlist/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWatchlist(string symbol, string? returnUrl, CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.Name ?? throw new InvalidOperationException("Authenticated user could not be resolved.");
        await marketDataService.ToggleWatchlistAsync(username, symbol, cancellationToken);

        if (IsAjaxRequest())
        {
            var watchlist = await marketDataService.GetWatchlistSymbolsAsync(username, cancellationToken);
            return Json(new
            {
                success = true,
                isFavorite = watchlist.Contains(symbol.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase)
            });
        }

        return RedirectToLocal(returnUrl, "/customer/market");
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost("/owner/market/providers/update")]
    [HttpPost("/admin/market/providers/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProvider(UpdateMarketProviderDto request, CancellationToken cancellationToken = default)
    {
        await marketDataService.UpdateProviderAsync(request, cancellationToken);
        TempData["Success"] = "Market provider ayarlari güncellendi.";
        return Redirect(AdminMarketPath);
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost("/owner/market/refresh")]
    [HttpPost("/admin/market/refresh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
    {
        var result = await marketDataService.RefreshAsync(force: true, cancellationToken);

        if (IsAjaxRequest())
        {
            return Json(result);
        }

        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return Redirect(AdminMarketPath);
    }

    private async Task<MarketBoardPageViewModel> BuildPageModelAsync(
        string title,
        string description,
        bool isAdmin,
        string baseCurrency,
        CancellationToken cancellationToken)
    {
        var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        var dashboard = await marketDataService.GetDashboardAsync(baseCurrency, username, cancellationToken);

        return new MarketBoardPageViewModel
        {
            Title = title,
            Description = description,
            IsAdmin = isAdmin,
            Dashboard = dashboard,
            DashboardApiUrl = Url.Action(nameof(Dashboard), values: new { baseCurrency = dashboard.BaseCurrency }) ?? "/api/market/dashboard",
            DetailApiUrlTemplate = "/api/market/quotes/__symbol__",
            WatchlistToggleUrl = "/customer/market/watchlist/toggle",
            HubUrl = "/hubs/market"
        };
    }

    private bool IsAjaxRequest()
        => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private IActionResult RedirectToLocal(string? returnUrl, string fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect(fallbackPath);
    }
}
