using MGold.Domain.Enums;

namespace MGold.Application.DTOs;

public class MarketDataSettings
{
    public const string SectionName = "MarketData";

    public string TcmbRatesUrl { get; set; } = "https://www.tcmb.gov.tr/kurlar/today.xml";
    public string YahooChartBaseUrl { get; set; } = "https://query1.finance.yahoo.com/v8/finance/chart";
    public string YahooInterval { get; set; } = "5m";
    public string YahooRange { get; set; } = "1d";
    public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; MGoldMarketBot/1.0)";
    public int DefaultRefreshIntervalSeconds { get; set; } = 45;
    public int CacheSeconds { get; set; } = 15;
}

public class MarketDashboardDto
{
    public string BaseCurrency { get; set; } = "TRY";
    public DateTime LastUpdatedAt { get; set; }
    public bool IsFallback { get; set; }
    public int RefreshIntervalSeconds { get; set; }
    public IReadOnlyList<string> AvailableCurrencies { get; set; } = [];
    public IReadOnlyList<MarketCategoryTabDto> Categories { get; set; } = [];
    public IReadOnlyList<MarketQuoteDto> Quotes { get; set; } = [];
    public IReadOnlyList<MarketQuoteDto> Watchlist { get; set; } = [];
    public IReadOnlyList<MarketQuoteDto> TopMovers { get; set; } = [];
    public IReadOnlyList<MarketProviderStatusDto> Providers { get; set; } = [];
}

public class MarketCategoryTabDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MarketQuoteDto
{
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public MarketCategory Category { get; set; }
    public string CategoryKey { get; set; } = string.Empty;
    public string CategoryLabel { get; set; } = string.Empty;
    public string DisplayCurrency { get; set; } = "TRY";
    public string NativeCurrency { get; set; } = "USD";
    public string UnitLabel { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Open24h { get; set; }
    public decimal High24h { get; set; }
    public decimal Low24h { get; set; }
    public decimal Change24hPercent { get; set; }
    public decimal Change24hAbsolute { get; set; }
    public bool IsRising { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsFallback { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public IReadOnlyList<decimal> Sparkline { get; set; } = [];
}

public class MarketQuoteDetailDto : MarketQuoteDto
{
    public string DetailSummary { get; set; } = string.Empty;
    public string TrendLabel { get; set; } = string.Empty;
}

public class MarketProviderStatusDto
{
    public int Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool SupportsRealtime { get; set; }
    public int Priority { get; set; }
    public int RefreshIntervalSeconds { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime? LastSuccessfulSyncAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string? LastError { get; set; }
    public int FailureCount { get; set; }
}

public class UpdateMarketProviderDto
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; }
    public bool SupportsRealtime { get; set; }
    public int Priority { get; set; }
    public int RefreshIntervalSeconds { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class MarketRefreshResultDto
{
    public bool Succeeded { get; set; }
    public bool UsedFallback { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public DateTime LastUpdatedAt { get; set; }
    public int QuoteCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MarketProviderPayloadDto
{
    public string ProviderKey { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public bool IsFallback { get; set; }
    public string? Note { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public IReadOnlyList<MarketProviderQuoteDto> Quotes { get; set; } = [];
}

public class MarketProviderQuoteDto
{
    public string Symbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MarketCategory Category { get; set; }
    public string UnitLabel { get; set; } = string.Empty;
    public string NativeCurrency { get; set; } = "USD";
    public decimal PriceInUsd { get; set; }
    public decimal? Price24hAgoInUsd { get; set; }
    public decimal? High24hInUsd { get; set; }
    public decimal? Low24hInUsd { get; set; }
    public int SortOrder { get; set; }
    public string? Note { get; set; }
}

public class MarketPulseDto
{
    public DateTime LastUpdatedAt { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public bool UsedFallback { get; set; }
}

public class MarketBoardPageViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public MarketDashboardDto Dashboard { get; set; } = new();
    public string DashboardApiUrl { get; set; } = string.Empty;
    public string DetailApiUrlTemplate { get; set; } = string.Empty;
    public string WatchlistToggleUrl { get; set; } = string.Empty;
    public string HubUrl { get; set; } = string.Empty;
}
