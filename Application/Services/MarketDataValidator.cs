using MGold.Application.DTOs;
using MGold.Application.Interfaces;

namespace MGold.Application.Services;

public class MarketDataValidator(ILogger<MarketDataValidator> logger) : IMarketDataValidator
{
    private static readonly HashSet<string> AllowedSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "live_market",
        "local_reference",
        "derived_formula",
        "manual_fallback"
    };

    public MarketProviderQuoteDto NormalizeProviderQuote(MarketProviderPayloadDto payload, MarketProviderQuoteDto quote)
    {
        var warnings = quote.QualityWarnings.ToList();

        if (quote.PriceInUsd <= 0)
        {
            warnings.Add("price pozitif değil");
        }

        var high = quote.High24hInUsd ?? quote.PriceInUsd;
        var low = quote.Low24hInUsd ?? quote.PriceInUsd;

        if (quote.PriceInUsd > 0 && high < quote.PriceInUsd)
        {
            warnings.Add("24s yüksek güncel fiyat altında kaldigi için düzeltildi");
            high = quote.PriceInUsd;
        }

        if (quote.PriceInUsd > 0 && low > quote.PriceInUsd)
        {
            warnings.Add("24s düşük güncel fiyat üstünde kaldigi için düzeltildi");
            low = quote.PriceInUsd;
        }

        if (high < low)
        {
            warnings.Add("24s yüksek/düşük aralığı ters geldiği için düzeltildi");
            (high, low) = (low, high);
        }

        if (payload.FetchedAt <= DateTime.UnixEpoch || payload.FetchedAt > DateTime.UtcNow.AddMinutes(5))
        {
            warnings.Add("timestamp geçersiz veya gelecekte");
        }

        if (string.IsNullOrWhiteSpace(payload.ProviderDisplayName) && string.IsNullOrWhiteSpace(quote.Note))
        {
            warnings.Add("source bos");
        }

        if (string.IsNullOrWhiteSpace(quote.SourceType) || !AllowedSourceTypes.Contains(quote.SourceType))
        {
            warnings.Add("sourceType dogrulanamadi");
            quote.SourceType = payload.IsFallback ? "manual_fallback" : "live_market";
        }

        if (quote.Price24hAgoInUsd is > 0)
        {
            var changeValue = quote.PriceInUsd - quote.Price24hAgoInUsd.Value;
            if (Math.Abs(changeValue) < 0.000001m)
            {
                quote.Price24hAgoInUsd = quote.PriceInUsd;
            }
        }

        quote.High24hInUsd = high;
        quote.Low24hInUsd = low;
        quote.QualityWarnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        quote.DataQualityStatus = ResolveQualityStatus(quote);

        foreach (var warning in quote.QualityWarnings)
        {
            logger.LogWarning(
                "Market quote validation warning for {Symbol}: {Warning}. Current={Current}, High={High}, Low={Low}, SourceType={SourceType}",
                quote.Symbol,
                warning,
                quote.PriceInUsd,
                quote.High24hInUsd,
                quote.Low24hInUsd,
                quote.SourceType);
        }

        return quote;
    }

    public MarketQuoteDto NormalizeMappedQuote(MarketQuoteDto quote)
    {
        var warnings = quote.QualityWarnings.ToList();

        if (quote.Price <= 0)
        {
            warnings.Add("price pozitif değil");
        }

        if (quote.High24h < quote.Price)
        {
            warnings.Add("24s yüksek güncel fiyata göre düzeltildi");
            quote.High24h = quote.Price;
        }

        if (quote.Low24h > quote.Price)
        {
            warnings.Add("24s düşük güncel fiyata göre düzeltildi");
            quote.Low24h = quote.Price;
        }

        if (quote.High24h < quote.Low24h)
        {
            warnings.Add("24s yüksek/düşük aralığı ters geldiği için düzeltildi");
            (quote.High24h, quote.Low24h) = (quote.Low24h, quote.High24h);
        }

        if (quote.Change24hAbsolute > 0 && quote.Change24hPercent < 0
            || quote.Change24hAbsolute < 0 && quote.Change24hPercent > 0)
        {
            warnings.Add("changePercent ve changeValue isaretleri uyumsuz");
            quote.Change24hPercent *= -1;
        }

        if (quote.LastUpdatedAt <= DateTime.UnixEpoch || quote.LastUpdatedAt > DateTime.UtcNow.AddMinutes(5))
        {
            warnings.Add("timestamp geçersiz veya gelecekte");
        }

        if (string.IsNullOrWhiteSpace(quote.ProviderDisplayName) && string.IsNullOrWhiteSpace(quote.Note))
        {
            warnings.Add("source bos");
        }

        if (string.IsNullOrWhiteSpace(quote.SourceType) || !AllowedSourceTypes.Contains(quote.SourceType))
        {
            warnings.Add("sourceType dogrulanamadi");
            quote.SourceType = quote.IsFallback ? "manual_fallback" : "live_market";
        }

        quote.QualityWarnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        quote.DataQualityStatus = ResolveQualityStatus(quote);
        return quote;
    }

    public void WarnIfGoldOunceMismatch(MarketProviderPayloadDto payload)
    {
        var tryQuote = payload.Quotes.FirstOrDefault(x => string.Equals(x.Symbol, "TRY", StringComparison.OrdinalIgnoreCase));
        var ounceQuote = payload.Quotes.FirstOrDefault(x => string.Equals(x.Symbol, "ONS_ALTIN", StringComparison.OrdinalIgnoreCase));
        var gramQuote = payload.Quotes.FirstOrDefault(x => string.Equals(x.Symbol, "GRAM_ALTIN", StringComparison.OrdinalIgnoreCase));

        if (tryQuote is null || ounceQuote is null || gramQuote is null
            || tryQuote.PriceInUsd <= 0 || ounceQuote.PriceInUsd <= 0 || gramQuote.PriceInUsd <= 0)
        {
            return;
        }

        var ounceTry = ounceQuote.PriceInUsd / tryQuote.PriceInUsd;
        var gramTry = gramQuote.PriceInUsd / tryQuote.PriceInUsd;
        var theoreticalGramTry = ounceTry / MarketCatalog.TroyOunceInGrams;
        if (theoreticalGramTry <= 0)
        {
            return;
        }

        var differencePercent = Math.Abs(gramTry - theoreticalGramTry) / theoreticalGramTry * 100m;
        if (differencePercent > 1m)
        {
            logger.LogWarning(
                "Gold ounce/gram consistency warning. ONS_ALTIN TRY={OunceTry}, theoretical gram TRY={TheoreticalGramTry}, GRAM_ALTIN TRY={GramTry}, difference={DifferencePercent:N2}%. Local spread may explain this.",
                ounceTry,
                theoreticalGramTry,
                gramTry,
                differencePercent);
        }
    }

    private static string ResolveQualityStatus(MarketProviderQuoteDto quote)
        => quote.PriceInUsd <= 0
            ? "error"
            : quote.QualityWarnings.Count > 0 ? "warning" : "ok";

    private static string ResolveQualityStatus(MarketQuoteDto quote)
        => quote.Price <= 0
            ? "error"
            : quote.QualityWarnings.Count > 0 ? "warning" : "ok";
}
