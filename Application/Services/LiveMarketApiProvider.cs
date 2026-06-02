using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Enums;

namespace MGold.Application.Services;

public class LiveMarketApiProvider(
    HttpClient httpClient,
    IOptions<MarketDataSettings> settings,
    ILogger<LiveMarketApiProvider> logger) : IMarketDataProvider
{
    public string ProviderKey => "live-api";

    private static readonly string[] YahooSymbols =
    [
        "GC=F",
        "SI=F",
        "PL=F",
        "PA=F",
        "HG=F",
        "BZ=F",
        "NG=F",
        "USDTRY=X",
        "EURUSD=X",
        "GBPUSD=X",
        "USDCHF=X",
        "USDJPY=X",
        "USDCAD=X",
        "USDSAR=X",
        "USDAED=X",
        "BTC-USD",
        "ETH-USD",
        "SOL-USD"
    ];

    public async Task<MarketProviderPayloadDto> FetchAsync(CancellationToken cancellationToken = default)
    {
        var tcmbTask = TryFetchTcmbRatesAsync(cancellationToken);
        var yahooTask = FetchYahooQuotesAsync(cancellationToken);
        await Task.WhenAll(tcmbTask, yahooTask);

        if (yahooTask.Result.Count == 0 && tcmbTask.Result.Count == 0)
        {
            throw new InvalidOperationException("Canli piyasa endpointlerinden gecerli veri alinamadi.");
        }

        var quotes = BuildQuotes(yahooTask.Result, tcmbTask.Result);
        if (quotes.Count == 0)
        {
            throw new InvalidOperationException("Canli piyasa endpointlerinden gecerli veri alinamadi.");
        }

        return new MarketProviderPayloadDto
        {
            ProviderKey = ProviderKey,
            ProviderDisplayName = "Yahoo Finance + TCMB",
            IsFallback = false,
            Note = "Gercek zamanli piyasa akisi",
            FetchedAt = DateTime.UtcNow,
            Quotes = quotes
        };
    }

    private async Task<Dictionary<string, SourceQuote>> FetchYahooQuotesAsync(CancellationToken cancellationToken)
    {
        var tasks = YahooSymbols.Select(symbol => TryFetchYahooChartAsync(symbol, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks);

        return results
            .Where(result => result is not null)
            .Select(result => result!)
            .ToDictionary(result => result.Symbol, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<SourceQuote?> TryFetchYahooChartAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{settings.Value.YahooChartBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(symbol)}?interval={Uri.EscapeDataString(settings.Value.YahooInterval)}&range={Uri.EscapeDataString(settings.Value.YahooRange)}");
            request.Headers.TryAddWithoutValidation("User-Agent", settings.Value.UserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var resultNode = document.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];
            var meta = resultNode.GetProperty("meta");
            var points = ExtractPoints(resultNode);
            var current = TryGetDecimal(meta, "regularMarketPrice") ?? points.LastOrDefault();
            var previousClose = TryGetDecimal(meta, "chartPreviousClose")
                ?? TryGetDecimal(meta, "previousClose")
                ?? points.FirstOrDefault();

            if (current <= 0 || previousClose <= 0)
            {
                return null;
            }

            var high = TryGetDecimal(meta, "regularMarketDayHigh") ?? (points.Count == 0 ? current : points.Max());
            var low = TryGetDecimal(meta, "regularMarketDayLow") ?? (points.Count == 0 ? current : points.Min());
            var currency = meta.TryGetProperty("currency", out var currencyNode)
                ? (currencyNode.GetString() ?? "USD").ToUpperInvariant()
                : "USD";
            var asOfUtc = meta.TryGetProperty("regularMarketTime", out var marketTimeNode)
                && marketTimeNode.TryGetInt64(out var unixTime)
                ? DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime
                : DateTime.UtcNow;

            return new SourceQuote(
                symbol,
                currency,
                current,
                previousClose,
                high,
                low,
                points.Count == 0 ? [current] : points,
                asOfUtc);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yahoo market endpoint failed for {Symbol}.", symbol);
            return null;
        }
    }

    private async Task<Dictionary<string, decimal>> TryFetchTcmbRatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, settings.Value.TcmbRatesUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", settings.Value.UserAgent);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = XDocument.Load(stream);
            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var currencyNode in document.Descendants("Currency"))
            {
                var code = currencyNode.Attribute("CurrencyCode")?.Value?.Trim().ToUpperInvariant();
                var unit = ParseTcmbDecimal(currencyNode.Element("Unit")?.Value) ?? 1m;
                var selling = ParseTcmbDecimal(currencyNode.Element("ForexSelling")?.Value)
                    ?? ParseTcmbDecimal(currencyNode.Element("ForexBuying")?.Value);

                if (string.IsNullOrWhiteSpace(code) || unit <= 0 || selling is null || selling <= 0)
                {
                    continue;
                }

                rates[code] = selling.Value / unit;
            }

            return rates;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TCMB exchange rates endpoint is unavailable.");
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private List<MarketProviderQuoteDto> BuildQuotes(
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        var quotes = new List<MarketProviderQuoteDto>();

        AddCurrencyQuotes(quotes, yahooQuotes, tcmbRates);
        AddGoldQuotes(quotes, yahooQuotes);
        AddMetalAndCommodityQuotes(quotes, yahooQuotes);
        AddCryptoQuotes(quotes, yahooQuotes);

        return quotes
            .OrderBy(quote => quote.SortOrder)
            .ThenBy(quote => quote.DisplayName)
            .ToList();
    }

    private void AddCurrencyQuotes(
        ICollection<MarketProviderQuoteDto> quotes,
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        var usdTry = ResolveDirectOrTcmb("TRY", yahooQuotes, tcmbRates);
        var eur = ResolveDirectOrTcmb("EUR", yahooQuotes, tcmbRates);
        var gbp = ResolveDirectOrTcmb("GBP", yahooQuotes, tcmbRates);
        var chf = ResolveInverseOrTcmb("CHF", yahooQuotes, tcmbRates);
        var jpy = ResolveInverseOrTcmb("JPY", yahooQuotes, tcmbRates);
        var cad = ResolveInverseOrTcmb("CAD", yahooQuotes, tcmbRates);
        var sar = ResolveInverseOrTcmb("SAR", yahooQuotes, tcmbRates);
        var aed = ResolveInverseOrTcmb("AED", yahooQuotes, tcmbRates);

        if (usdTry is not null)
        {
            quotes.Add(ToProviderQuote("TRY", "Turk Lirasi", MarketCategory.Currency, "1 birim", 10, Invert(usdTry), "Yahoo Finance / TCMB"));
        }

        quotes.Add(CreateConstantCurrencyQuote("USD", "Amerikan Dolari", 11));

        if (eur is not null)
        {
            quotes.Add(ToProviderQuote("EUR", "Euro", MarketCategory.Currency, "1 birim", 12, eur, "Yahoo Finance / TCMB"));
        }

        if (gbp is not null)
        {
            quotes.Add(ToProviderQuote("GBP", "Sterlin", MarketCategory.Currency, "1 birim", 13, gbp, "Yahoo Finance / TCMB"));
        }

        if (chf is not null)
        {
            quotes.Add(ToProviderQuote("CHF", "Isvicre Frangi", MarketCategory.Currency, "1 birim", 14, chf, "Yahoo Finance / TCMB"));
        }

        if (jpy is not null)
        {
            quotes.Add(ToProviderQuote("JPY", "Japon Yeni", MarketCategory.Currency, "1 birim", 15, jpy, "Yahoo Finance / TCMB"));
        }

        if (sar is not null)
        {
            quotes.Add(ToProviderQuote("SAR", "Suudi Riyali", MarketCategory.Currency, "1 birim", 16, sar, "Yahoo Finance / TCMB"));
        }

        if (aed is not null)
        {
            quotes.Add(ToProviderQuote("AED", "BAE Dirhemi", MarketCategory.Currency, "1 birim", 17, aed, "Yahoo Finance / TCMB"));
        }

        if (cad is not null)
        {
            quotes.Add(ToProviderQuote("CAD", "Kanada Dolari", MarketCategory.Currency, "1 birim", 18, cad, "Yahoo Finance / TCMB"));
        }
    }

    private void AddGoldQuotes(ICollection<MarketProviderQuoteDto> quotes, IReadOnlyDictionary<string, SourceQuote> yahooQuotes)
    {
        if (!yahooQuotes.TryGetValue("GC=F", out var ounceGold))
        {
            return;
        }

        var pureGram = Scale(ounceGold, 1m / MarketCatalog.TroyOunceInGrams);
        var gramAltin = Scale(ounceGold, 0.995m / MarketCatalog.TroyOunceInGrams);

        quotes.Add(ToProviderQuote("ONS_ALTIN", "Ons Altin", MarketCategory.Gold, "ons", 1, ounceGold, "COMEX Gold Futures"));
        quotes.Add(ToProviderQuote("GRAM_ALTIN", "Gram Altin", MarketCategory.Gold, "gram", 2, gramAltin, "COMEX Gold Futures x saflik"));
        quotes.Add(ToProviderQuote("CEYREK_ALTIN", "Ceyrek Altin", MarketCategory.Gold, "adet", 3, Scale(ounceGold, MarketCatalog.QuarterGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi"));
        quotes.Add(ToProviderQuote("YARIM_ALTIN", "Yarim Altin", MarketCategory.Gold, "adet", 4, Scale(ounceGold, MarketCatalog.HalfGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi"));
        quotes.Add(ToProviderQuote("TAM_ALTIN", "Tam Altin", MarketCategory.Gold, "adet", 5, Scale(ounceGold, MarketCatalog.FullGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi"));
        quotes.Add(ToProviderQuote("CUMHURIYET_ALTIN", "Cumhuriyet Altini", MarketCategory.Gold, "adet", 6, Scale(ounceGold, MarketCatalog.RepublicGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi"));
        quotes.Add(ToProviderQuote("ALTIN_14K", "14 Ayar Altin", MarketCategory.Gold, "gram", 7, Scale(pureGram, 14m / 24m), "Spot altin saflik katsayisi"));
        quotes.Add(ToProviderQuote("ALTIN_18K", "18 Ayar Altin", MarketCategory.Gold, "gram", 8, Scale(pureGram, 18m / 24m), "Spot altin saflik katsayisi"));
        quotes.Add(ToProviderQuote("ALTIN_22K", "22 Ayar Altin", MarketCategory.Gold, "gram", 9, Scale(pureGram, 22m / 24m), "Spot altin saflik katsayisi"));
        quotes.Add(ToProviderQuote("HAS_ALTIN", "Has Altin", MarketCategory.Gold, "gram", 10, Scale(pureGram, 0.999m), "Spot altin saflik katsayisi"));
    }

    private void AddMetalAndCommodityQuotes(ICollection<MarketProviderQuoteDto> quotes, IReadOnlyDictionary<string, SourceQuote> yahooQuotes)
    {
        if (yahooQuotes.TryGetValue("SI=F", out var silver))
        {
            quotes.Add(ToProviderQuote("GUMUS", "Gumus", MarketCategory.Metal, "ons", 19, silver, "COMEX Silver Futures"));
        }

        if (yahooQuotes.TryGetValue("PL=F", out var platinum))
        {
            quotes.Add(ToProviderQuote("PLATINUM", "Platin", MarketCategory.Metal, "ons", 20, platinum, "NYMEX Platinum Futures"));
        }

        if (yahooQuotes.TryGetValue("PA=F", out var palladium))
        {
            quotes.Add(ToProviderQuote("PALLADIUM", "Paladyum", MarketCategory.Metal, "ons", 21, palladium, "NYMEX Palladium Futures"));
        }

        if (yahooQuotes.TryGetValue("HG=F", out var copper))
        {
            quotes.Add(ToProviderQuote("COPPER", "Bakir", MarketCategory.Metal, "libre", 22, copper, "COMEX Copper Futures"));
        }

        if (yahooQuotes.TryGetValue("BZ=F", out var brent))
        {
            quotes.Add(ToProviderQuote("BRENT", "Brent Petrol", MarketCategory.Commodity, "varil", 23, brent, "ICE Brent Crude Futures"));
        }

        if (yahooQuotes.TryGetValue("NG=F", out var naturalGas))
        {
            quotes.Add(ToProviderQuote("NATGAS", "Dogalgaz", MarketCategory.Commodity, "mmbtu", 24, naturalGas, "NYMEX Natural Gas Futures"));
        }
    }

    private void AddCryptoQuotes(ICollection<MarketProviderQuoteDto> quotes, IReadOnlyDictionary<string, SourceQuote> yahooQuotes)
    {
        if (yahooQuotes.TryGetValue("BTC-USD", out var bitcoin))
        {
            quotes.Add(ToProviderQuote("BTC", "Bitcoin", MarketCategory.Crypto, "adet", 25, bitcoin, "Yahoo Finance Crypto"));
        }

        if (yahooQuotes.TryGetValue("ETH-USD", out var ethereum))
        {
            quotes.Add(ToProviderQuote("ETH", "Ethereum", MarketCategory.Crypto, "adet", 26, ethereum, "Yahoo Finance Crypto"));
        }

        if (yahooQuotes.TryGetValue("SOL-USD", out var solana))
        {
            quotes.Add(ToProviderQuote("SOL", "Solana", MarketCategory.Crypto, "adet", 27, solana, "Yahoo Finance Crypto"));
        }
    }

    private static MarketProviderQuoteDto CreateConstantCurrencyQuote(string symbol, string displayName, int sortOrder)
        => new()
        {
            Symbol = symbol,
            DisplayName = displayName,
            Category = MarketCategory.Currency,
            UnitLabel = "1 birim",
            NativeCurrency = "USD",
            PriceInUsd = 1m,
            Price24hAgoInUsd = 1m,
            High24hInUsd = 1m,
            Low24hInUsd = 1m,
            SortOrder = sortOrder,
            Note = "ABD Dolar referansi"
        };

    private static MarketProviderQuoteDto ToProviderQuote(
        string symbol,
        string displayName,
        MarketCategory category,
        string unitLabel,
        int sortOrder,
        SourceQuote source,
        string note)
        => new()
        {
            Symbol = symbol,
            DisplayName = displayName,
            Category = category,
            UnitLabel = unitLabel,
            NativeCurrency = "USD",
            PriceInUsd = RoundPrice(source.Current),
            Price24hAgoInUsd = RoundPrice(source.PreviousClose),
            High24hInUsd = RoundPrice(source.High24h),
            Low24hInUsd = RoundPrice(source.Low24h),
            SortOrder = sortOrder,
            Note = note
        };

    private static SourceQuote? ResolveDirectOrTcmb(
        string currencyCode,
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        var yahooSymbol = currencyCode switch
        {
            "TRY" => "USDTRY=X",
            "EUR" => "EURUSD=X",
            "GBP" => "GBPUSD=X",
            _ => null
        };

        if (yahooSymbol is not null && yahooQuotes.TryGetValue(yahooSymbol, out var direct))
        {
            return direct;
        }

        return TryBuildFromTcmb(currencyCode, tcmbRates);
    }

    private static SourceQuote? ResolveInverseOrTcmb(
        string currencyCode,
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        var yahooSymbol = currencyCode switch
        {
            "CHF" => "USDCHF=X",
            "JPY" => "USDJPY=X",
            "CAD" => "USDCAD=X",
            "SAR" => "USDSAR=X",
            "AED" => "USDAED=X",
            _ => null
        };

        if (yahooSymbol is not null && yahooQuotes.TryGetValue(yahooSymbol, out var inverse))
        {
            return Invert(inverse);
        }

        return TryBuildFromTcmb(currencyCode, tcmbRates);
    }

    private static SourceQuote? TryBuildFromTcmb(string currencyCode, IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        if (!tcmbRates.TryGetValue("USD", out var tryPerUsd) || tryPerUsd <= 0)
        {
            return null;
        }

        decimal usdPerUnit;
        if (string.Equals(currencyCode, "TRY", StringComparison.OrdinalIgnoreCase))
        {
            usdPerUnit = 1m / tryPerUsd;
        }
        else
        {
            if (!tcmbRates.TryGetValue(currencyCode, out var tryPerUnit) || tryPerUnit <= 0)
            {
                return null;
            }

            usdPerUnit = tryPerUnit / tryPerUsd;
        }

        return new SourceQuote(currencyCode, "USD", usdPerUnit, usdPerUnit, usdPerUnit, usdPerUnit, [usdPerUnit], DateTime.UtcNow);
    }

    private static SourceQuote Scale(SourceQuote source, decimal factor)
        => new(
            source.Symbol,
            source.Currency,
            source.Current * factor,
            source.PreviousClose * factor,
            source.High24h * factor,
            source.Low24h * factor,
            source.Points.Select(point => point * factor).ToList(),
            source.AsOfUtc);

    private static SourceQuote Invert(SourceQuote source)
    {
        static decimal SafeInverse(decimal value) => value <= 0 ? 0 : 1m / value;

        return new SourceQuote(
            source.Symbol,
            "USD",
            SafeInverse(source.Current),
            SafeInverse(source.PreviousClose),
            SafeInverse(source.Low24h),
            SafeInverse(source.High24h),
            source.Points.Where(point => point > 0).Select(SafeInverse).ToList(),
            source.AsOfUtc);
    }

    private static List<decimal> ExtractPoints(JsonElement resultNode)
    {
        var points = new List<decimal>();
        if (!resultNode.TryGetProperty("indicators", out var indicators)
            || !indicators.TryGetProperty("quote", out var quoteArray)
            || quoteArray.GetArrayLength() == 0)
        {
            return points;
        }

        var quote = quoteArray[0];
        if (!quote.TryGetProperty("close", out var closeArray))
        {
            return points;
        }

        foreach (var item in closeArray.EnumerateArray())
        {
            var value = TryReadDecimal(item);
            if (value is > 0)
            {
                points.Add(value.Value);
            }
        }

        return points;
    }

    private static decimal? TryGetDecimal(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) ? TryReadDecimal(property) : null;

    private static decimal? TryReadDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetDecimal(out var decimalValue))
            {
                return decimalValue;
            }

            if (element.TryGetDouble(out var doubleValue))
            {
                return Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
            }
        }

        if (element.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? ParseTcmbDecimal(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return decimal.TryParse(rawValue.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal RoundPrice(decimal value)
        => Math.Round(value, value >= 1000m ? 4 : 6);

    private sealed record SourceQuote(
        string Symbol,
        string Currency,
        decimal Current,
        decimal PreviousClose,
        decimal High24h,
        decimal Low24h,
        IReadOnlyList<decimal> Points,
        DateTime AsOfUtc);
}
