using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class MarketDataService(
    AppDbContext context,
    IMemoryCache cache,
    IEnumerable<IMarketDataProvider> providers,
    IOptions<MarketDataSettings> settings,
    IMarketDataValidator validator,
    ILogger<MarketDataService> logger) : IMarketDataService
{
    private const string DashboardCachePrefix = "market-dashboard:";
    private const string RefreshLockCacheKey = "market-refresh-lock";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MarketDashboardDto> GetDashboardAsync(string baseCurrency, string? username = null, CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = NormalizeCurrency(baseCurrency);
        var useCache = string.IsNullOrWhiteSpace(username);
        var cacheKey = $"{DashboardCachePrefix}{normalizedCurrency}:anon";
        if (useCache && cache.TryGetValue(cacheKey, out MarketDashboardDto? cached) && cached is not null)
        {
            return cached;
        }

        var snapshots = await context.MarketQuoteSnapshots
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            await RefreshAsync(force: true, cancellationToken);
            snapshots = await context.MarketQuoteSnapshots
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.DisplayName)
                .ToListAsync(cancellationToken);
        }

        var watchlist = string.IsNullOrWhiteSpace(username)
            ? []
            : await GetWatchlistSymbolsAsync(username, cancellationToken);

        var currentDisplayCurrencyUsd = ResolveCurrencyPriceInUsd(snapshots, normalizedCurrency, useHistorical: false);
        var historicalDisplayCurrencyUsd = ResolveCurrencyPriceInUsd(snapshots, normalizedCurrency, useHistorical: true);
        var quotes = snapshots
            .Where(snapshot => MarketCatalog.IsVisibleOnBoard(snapshot.Symbol))
            .Select(snapshot => MapQuote(
                snapshot,
                normalizedCurrency,
                currentDisplayCurrencyUsd,
                historicalDisplayCurrencyUsd,
                watchlist.Contains(snapshot.Symbol, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var dashboard = new MarketDashboardDto
        {
            BaseCurrency = normalizedCurrency,
            LastUpdatedAt = snapshots.Count == 0 ? DateTime.UtcNow : snapshots.Max(x => x.LastUpdatedAt),
            IsFallback = snapshots.Count > 0 && snapshots.All(x => x.IsFallback),
            RefreshIntervalSeconds = await ResolveRefreshIntervalAsync(cancellationToken),
            AvailableCurrencies = MarketCatalog.SupportedCurrencies,
            Categories = quotes
                .GroupBy(x => x.Category)
                .Select(group => new MarketCategoryTabDto
                {
                    Key = group.Key.ToString(),
                    Label = MarketCatalog.ToCategoryLabel(group.Key),
                    Count = group.Count()
                })
                .OrderBy(x => x.Label)
                .ToList(),
            Quotes = quotes,
            Watchlist = quotes.Where(x => x.IsFavorite).ToList(),
            TopMovers = quotes.OrderByDescending(x => Math.Abs(x.Change24hPercent)).Take(6).ToList(),
            Providers = await GetProviderStatusesAsync(cancellationToken)
        };

        if (useCache)
        {
            cache.Set(cacheKey, dashboard, TimeSpan.FromSeconds(settings.Value.CacheSeconds));
        }

        return dashboard;
    }

    public async Task<MarketQuoteDetailDto?> GetQuoteDetailAsync(string symbol, string baseCurrency, string? username = null, CancellationToken cancellationToken = default)
    {
        var dashboard = await GetDashboardAsync(baseCurrency, username, cancellationToken);
        var quote = dashboard.Quotes.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (quote is null)
        {
            return null;
        }

        return new MarketQuoteDetailDto
        {
            Symbol = quote.Symbol,
            DisplayName = quote.DisplayName,
            SearchText = quote.SearchText,
            Category = quote.Category,
            CategoryLabel = quote.CategoryLabel,
            DisplayCurrency = quote.DisplayCurrency,
            NativeCurrency = quote.NativeCurrency,
            UnitLabel = quote.UnitLabel,
            Price = quote.Price,
            Open24h = quote.Open24h,
            High24h = quote.High24h,
            Low24h = quote.Low24h,
            Change24hPercent = quote.Change24hPercent,
            Change24hAbsolute = quote.Change24hAbsolute,
            IsRising = quote.IsRising,
            IsFavorite = quote.IsFavorite,
            IsFallback = quote.IsFallback,
            ProviderKey = quote.ProviderKey,
            ProviderDisplayName = quote.ProviderDisplayName,
            Note = quote.Note,
            SourceType = quote.SourceType,
            CalculationBasis = quote.CalculationBasis,
            DataStatus = quote.DataStatus,
            DataQualityStatus = quote.DataQualityStatus,
            QualityWarnings = quote.QualityWarnings,
            LastUpdatedAt = quote.LastUpdatedAt,
            Sparkline = quote.Sparkline,
            DetailSummary = $"{quote.DisplayName} son 24 saatte {Math.Abs(quote.Change24hPercent):N2}% {(quote.IsRising ? "yukseldi" : "geriledi")}.",
            TrendLabel = quote.IsRising ? "Yukselis Egilimi" : "Geri Cekilme Egilimi"
        };
    }

    public async Task ToggleWatchlistAsync(string username, string symbol, CancellationToken cancellationToken = default)
    {
        var user = await context.AppUsers.FirstAsync(x => x.Username == username.ToLowerInvariant(), cancellationToken);
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var item = await context.MarketWatchlistItems
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id && x.Symbol == normalizedSymbol, cancellationToken);

        if (item is null)
        {
            await context.MarketWatchlistItems.AddAsync(new MarketWatchlistItem
            {
                AppUserId = user.Id,
                Symbol = normalizedSymbol,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            context.MarketWatchlistItems.Remove(item);
        }

        await context.SaveChangesAsync(cancellationToken);
        InvalidateDashboardCache();
    }

    public async Task<IReadOnlyList<string>> GetWatchlistSymbolsAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return await context.MarketWatchlistItems
            .AsNoTracking()
            .Where(x => x.AppUser.Username == normalized)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketProviderStatusDto>> GetProviderStatusesAsync(CancellationToken cancellationToken = default)
    {
        return await context.MarketProviderConfigurations
            .AsNoTracking()
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.DisplayName)
            .Select(x => new MarketProviderStatusDto
            {
                Id = x.Id,
                ProviderKey = x.ProviderKey,
                DisplayName = x.DisplayName,
                IsEnabled = x.IsEnabled,
                SupportsRealtime = x.SupportsRealtime,
                Priority = x.Priority,
                RefreshIntervalSeconds = x.RefreshIntervalSeconds,
                IsHealthy = x.FailureCount == 0 || x.LastFailureAt == null || (x.LastSuccessfulSyncAt.HasValue && x.LastSuccessfulSyncAt > x.LastFailureAt),
                LastSuccessfulSyncAt = x.LastSuccessfulSyncAt,
                LastFailureAt = x.LastFailureAt,
                LastError = x.LastError,
                FailureCount = x.FailureCount
            })
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateProviderAsync(UpdateMarketProviderDto request, CancellationToken cancellationToken = default)
    {
        var entity = await context.MarketProviderConfigurations.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Market provider bulunamadi.");

        entity.DisplayName = request.DisplayName.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.SupportsRealtime = request.SupportsRealtime;
        entity.Priority = request.Priority;
        entity.RefreshIntervalSeconds = Math.Clamp(request.RefreshIntervalSeconds, 10, 600);
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        InvalidateDashboardCache();
    }

    public async Task<MarketRefreshResultDto> RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && cache.TryGetValue(RefreshLockCacheKey, out MarketRefreshResultDto? cached) && cached is not null)
        {
            return cached;
        }

        var configs = await context.MarketProviderConfigurations
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var providerLookup = providers.ToDictionary(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var config in configs.Where(x => x.IsEnabled))
        {
            if (!providerLookup.TryGetValue(config.ProviderKey, out var provider))
            {
                errors.Add($"{config.ProviderKey}: provider kayitli degil");
                continue;
            }

            try
            {
                var payload = await provider.FetchAsync(cancellationToken);
                await PersistPayloadAsync(payload, cancellationToken);
                config.LastSuccessfulSyncAt = payload.FetchedAt;
                config.LastError = null;
                config.LastFailureAt = null;
                config.FailureCount = 0;
                config.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);

                var result = new MarketRefreshResultDto
                {
                    Succeeded = true,
                    UsedFallback = payload.IsFallback,
                    ProviderKey = payload.ProviderKey,
                    ProviderDisplayName = payload.ProviderDisplayName,
                    LastUpdatedAt = payload.FetchedAt,
                    QuoteCount = payload.Quotes.Count,
                    Message = payload.Note ?? "Market verileri guncellendi."
                };

                cache.Set(RefreshLockCacheKey, result, TimeSpan.FromSeconds(Math.Max(8, config.RefreshIntervalSeconds / 2)));
                InvalidateDashboardCache();
                return result;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Market provider {ProviderKey} failed.", config.ProviderKey);
                config.LastFailureAt = DateTime.UtcNow;
                config.LastError = ex.Message;
                config.FailureCount += 1;
                config.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                errors.Add($"{config.ProviderKey}: {ex.Message}");
            }
        }

        var storedCount = await context.MarketQuoteSnapshots.CountAsync(cancellationToken);
        var fallbackResult = new MarketRefreshResultDto
        {
            Succeeded = storedCount > 0,
            UsedFallback = true,
            ProviderKey = "cache-fallback",
            ProviderDisplayName = "Stored Cache",
            LastUpdatedAt = storedCount > 0
                ? await context.MarketQuoteSnapshots.MaxAsync(x => x.LastUpdatedAt, cancellationToken)
                : DateTime.UtcNow,
            QuoteCount = storedCount,
            Message = errors.Count == 0 ? "Kayitli market verisi kullanildi." : string.Join(" | ", errors)
        };

        cache.Set(RefreshLockCacheKey, fallbackResult, TimeSpan.FromSeconds(8));
        InvalidateDashboardCache();
        return fallbackResult;
    }

    private async Task PersistPayloadAsync(MarketProviderPayloadDto payload, CancellationToken cancellationToken)
    {
        var existing = await context.MarketQuoteSnapshots.ToListAsync(cancellationToken);
        var existingBySymbol = existing.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);
        var normalizedQuotes = payload.Quotes
            .Select(quote => validator.NormalizeProviderQuote(payload, quote))
            .Where(quote => quote.PriceInUsd > 0)
            .ToList();

        payload.Quotes = normalizedQuotes;
        validator.WarnIfGoldOunceMismatch(payload);

        foreach (var quote in normalizedQuotes)
        {
            if (!existingBySymbol.TryGetValue(quote.Symbol, out var entity))
            {
                entity = new MarketQuoteSnapshot
                {
                    Symbol = quote.Symbol,
                    CreatedAt = DateTime.UtcNow
                };
                context.MarketQuoteSnapshots.Add(entity);
                existingBySymbol[quote.Symbol] = entity;
            }

            var sparkline = ParseSparkline(entity.SparklineJson);
            sparkline.Add(Math.Round(quote.PriceInUsd, 4));
            if (sparkline.Count > 18)
            {
                sparkline = sparkline[^18..];
            }

            var oldPrice = quote.Price24hAgoInUsd ?? (sparkline.Count > 1 ? sparkline[0] : quote.PriceInUsd);

            entity.DisplayName = quote.DisplayName;
            entity.Category = quote.Category;
            entity.UnitLabel = quote.UnitLabel;
            entity.NativeCurrency = quote.NativeCurrency;
            entity.PriceInUsd = quote.PriceInUsd;
            entity.Price24hAgoInUsd = oldPrice;
            entity.High24hInUsd = quote.High24hInUsd ?? sparkline.Max();
            entity.Low24hInUsd = quote.Low24hInUsd ?? sparkline.Min();
            entity.SparklineJson = JsonSerializer.Serialize(sparkline, JsonOptions);
            entity.ProviderKey = payload.ProviderKey;
            entity.ProviderDisplayName = payload.ProviderDisplayName;
            entity.Note = quote.Note ?? payload.Note;
            entity.SourceType = quote.SourceType;
            entity.CalculationBasis = quote.CalculationBasis;
            entity.DataQualityStatus = quote.DataQualityStatus;
            entity.QualityWarningsJson = JsonSerializer.Serialize(quote.QualityWarnings, JsonOptions);
            entity.IsFallback = payload.IsFallback;
            entity.SortOrder = quote.SortOrder;
            entity.LastUpdatedAt = payload.FetchedAt;
        }

        await SyncGoldPriceFromPayloadAsync(payload, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncGoldPriceFromPayloadAsync(MarketProviderPayloadDto payload, CancellationToken cancellationToken)
    {
        var tryQuote = payload.Quotes.FirstOrDefault(x => string.Equals(x.Symbol, "TRY", StringComparison.OrdinalIgnoreCase));
        var gramQuote = payload.Quotes.FirstOrDefault(x => string.Equals(x.Symbol, "GRAM_ALTIN", StringComparison.OrdinalIgnoreCase));
        if (tryQuote is null || gramQuote is null || tryQuote.PriceInUsd <= 0 || gramQuote.PriceInUsd <= 0)
        {
            return;
        }

        var gramTry = Math.Round(gramQuote.PriceInUsd / tryQuote.PriceInUsd, 2, MidpointRounding.AwayFromZero);
        if (gramTry <= 0)
        {
            return;
        }

        var latest = await context.GoldPrices
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var threshold = Math.Max(0.01m, settings.Value.GoldPriceSyncThresholdTry);
        if (latest is not null && Math.Abs(latest.PricePerGram - gramTry) < threshold)
        {
            return;
        }

        var activePrices = await context.GoldPrices.Where(x => x.IsActive).ToListAsync(cancellationToken);
        foreach (var activePrice in activePrices)
        {
            activePrice.IsActive = false;
        }

        context.GoldPrices.Add(new GoldPrice
        {
            PricePerGram = gramTry,
            EffectiveFrom = payload.FetchedAt,
            Source = TrimSource($"{payload.ProviderDisplayName} - {gramQuote.Note}"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private MarketQuoteDto MapQuote(
        MarketQuoteSnapshot snapshot,
        string displayCurrency,
        decimal currentDisplayCurrencyUsd,
        decimal historicalDisplayCurrencyUsd,
        bool isFavorite)
    {
        var current = ConvertFromUsd(snapshot.PriceInUsd, currentDisplayCurrencyUsd);
        var open24 = ConvertFromUsd(snapshot.Price24hAgoInUsd, historicalDisplayCurrencyUsd);
        var high24 = ConvertFromUsd(snapshot.High24hInUsd, currentDisplayCurrencyUsd);
        var low24 = ConvertFromUsd(snapshot.Low24hInUsd, currentDisplayCurrencyUsd);
        var changeAbs = current - open24;
        var changePercent = open24 == 0 ? 0 : (changeAbs / open24) * 100m;
        var sparkline = ParseSparkline(snapshot.SparklineJson).Select(x => ConvertFromUsd(x, currentDisplayCurrencyUsd)).ToList();

        var quote = new MarketQuoteDto
        {
            Symbol = snapshot.Symbol,
            DisplayName = snapshot.DisplayName,
            SearchText = $"{snapshot.DisplayName} {snapshot.Symbol} {MarketCatalog.ToCategoryLabel(snapshot.Category)}".ToLowerInvariant(),
            Category = snapshot.Category,
            CategoryKey = snapshot.Category.ToString(),
            CategoryLabel = MarketCatalog.ToCategoryLabel(snapshot.Category),
            DisplayCurrency = displayCurrency,
            NativeCurrency = snapshot.NativeCurrency,
            UnitLabel = snapshot.UnitLabel,
            Price = current,
            Open24h = open24,
            High24h = high24,
            Low24h = low24,
            Change24hAbsolute = changeAbs,
            Change24hPercent = changePercent,
            IsRising = changePercent >= 0,
            IsFavorite = isFavorite,
            IsFallback = snapshot.IsFallback,
            ProviderKey = snapshot.ProviderKey,
            ProviderDisplayName = snapshot.ProviderDisplayName,
            Note = snapshot.Note,
            SourceType = string.IsNullOrWhiteSpace(snapshot.SourceType) ? (snapshot.IsFallback ? "manual_fallback" : "live_market") : snapshot.SourceType,
            CalculationBasis = snapshot.CalculationBasis,
            DataStatus = ResolveDataStatus(snapshot),
            DataQualityStatus = string.IsNullOrWhiteSpace(snapshot.DataQualityStatus) ? "ok" : snapshot.DataQualityStatus,
            QualityWarnings = ParseWarnings(snapshot.QualityWarningsJson),
            LastUpdatedAt = snapshot.LastUpdatedAt,
            Sparkline = sparkline
        };

        return validator.NormalizeMappedQuote(quote);
    }

    private static List<decimal> ParseSparkline(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<decimal>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<string> ParseWarnings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return ["Kaynak dogrulanamadi"];
        }
    }

    private async Task<int> ResolveRefreshIntervalAsync(CancellationToken cancellationToken)
    {
        var configured = await context.MarketProviderConfigurations
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Select(x => x.RefreshIntervalSeconds)
            .ToListAsync(cancellationToken);

        return configured.Count == 0
            ? settings.Value.DefaultRefreshIntervalSeconds
            : configured.Min();
    }

    private static decimal ConvertFromUsd(decimal usdPrice, decimal displayCurrencyUsd)
        => displayCurrencyUsd == 0 ? usdPrice : Math.Round(usdPrice / displayCurrencyUsd, 4);

    private static string NormalizeCurrency(string currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();
        return MarketCatalog.SupportedCurrencies.Contains(normalized, StringComparer.OrdinalIgnoreCase) ? normalized : "TRY";
    }

    private static decimal ResolveCurrencyPriceInUsd(IReadOnlyList<MarketQuoteSnapshot> snapshots, string targetCurrency, bool useHistorical)
    {
        if (string.Equals(targetCurrency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var snapshot = snapshots.FirstOrDefault(x => x.Symbol == targetCurrency);
        if (snapshot is null)
        {
            return 1m;
        }

        var value = useHistorical ? snapshot.Price24hAgoInUsd : snapshot.PriceInUsd;
        return value <= 0 ? 1m : value;
    }

    private string ResolveDataStatus(MarketQuoteSnapshot snapshot)
    {
        if (string.Equals(snapshot.DataQualityStatus, "error", StringComparison.OrdinalIgnoreCase) || snapshot.PriceInUsd <= 0)
        {
            return "error";
        }

        var age = DateTime.UtcNow - snapshot.LastUpdatedAt;
        if (age <= TimeSpan.FromSeconds(Math.Max(60, settings.Value.DefaultRefreshIntervalSeconds * 2)) && !snapshot.IsFallback)
        {
            return "active/realtime";
        }

        if (age <= TimeSpan.FromMinutes(15))
        {
            return "recentSnapshot";
        }

        return "stale";
    }

    private void InvalidateDashboardCache()
    {
        foreach (var currency in MarketCatalog.SupportedCurrencies)
        {
            cache.Remove($"{DashboardCachePrefix}{currency}:anon");
        }
    }

    private static string TrimSource(string source)
        => source.Length <= 100 ? source : source[..100];
}
