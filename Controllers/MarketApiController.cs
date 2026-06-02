using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;
using MGold.Domain.Constants;

namespace MGold.Controllers;

[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Route("api/market/v2")]
public class MarketApiController(IMarketDataService marketDataService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("dashboard-v2")]
    public async Task<IActionResult> GetDashboard([FromQuery] string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        return ApiResponseFactory.Create(this, await marketDataService.GetDashboardAsync(baseCurrency, username, cancellationToken), "Market dashboard retrieved successfully.");
    }

    [AllowAnonymous]
    [HttpGet("quotes/{symbol}")]
    public async Task<IActionResult> GetQuote(string symbol, [FromQuery] string baseCurrency = "TRY", CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        var detail = await marketDataService.GetQuoteDetailAsync(symbol, baseCurrency, username, cancellationToken)
            ?? throw new KeyNotFoundException($"Market quote {symbol} was not found.");
        return ApiResponseFactory.Create(this, detail, "Market quote retrieved successfully.");
    }

    [Authorize(Roles = RoleConstants.CustomerOnly)]
    [HttpPost("watchlist/{symbol}/toggle")]
    public async Task<IActionResult> ToggleWatchlist(string symbol, CancellationToken cancellationToken = default)
    {
        var username = User.Identity?.Name ?? throw new InvalidOperationException("Authenticated user could not be resolved.");
        await marketDataService.ToggleWatchlistAsync(username, symbol, cancellationToken);
        var normalized = symbol.Trim().ToUpperInvariant();
        var watchlist = await marketDataService.GetWatchlistSymbolsAsync(username, cancellationToken);
        return ApiResponseFactory.Create(this, new ToggleMarketWatchlistResultDto
        {
            Symbol = normalized,
            IsFavorite = watchlist.Contains(normalized, StringComparer.OrdinalIgnoreCase)
        }, "Market watchlist updated successfully.");
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken = default)
        => ApiResponseFactory.Create(this, await marketDataService.GetProviderStatusesAsync(cancellationToken), "Market providers retrieved successfully.");

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPut("providers/{id:int}")]
    public async Task<IActionResult> UpdateProvider(int id, [FromBody] UpdateMarketProviderDto dto, CancellationToken cancellationToken = default)
    {
        dto.Id = id;
        await marketDataService.UpdateProviderAsync(dto, cancellationToken);
        return ApiResponseFactory.Create(this, new { id }, "Market provider updated successfully.");
    }

    [Authorize(Roles = RoleConstants.ManagerOrSystemAdmin)]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
        => ApiResponseFactory.Create(this, await marketDataService.RefreshAsync(force: true, cancellationToken), "Market data refresh completed.");
}
