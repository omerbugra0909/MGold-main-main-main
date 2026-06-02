using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IMarketDataService
{
    Task<MarketDashboardDto> GetDashboardAsync(string baseCurrency, string? username = null, CancellationToken cancellationToken = default);
    Task<MarketQuoteDetailDto?> GetQuoteDetailAsync(string symbol, string baseCurrency, string? username = null, CancellationToken cancellationToken = default);
    Task ToggleWatchlistAsync(string username, string symbol, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetWatchlistSymbolsAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketProviderStatusDto>> GetProviderStatusesAsync(CancellationToken cancellationToken = default);
    Task UpdateProviderAsync(UpdateMarketProviderDto request, CancellationToken cancellationToken = default);
    Task<MarketRefreshResultDto> RefreshAsync(bool force = false, CancellationToken cancellationToken = default);
}
