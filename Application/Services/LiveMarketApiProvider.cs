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
        "AUDUSD=X",
        "NZDUSD=X",
        "USDMXN=X",
        "USDNOK=X",
        "USDSEK=X",
        "USDDKK=X",
        "USDZAR=X",
        "USDSGD=X",
        "USDHKD=X",
        "USDCNY=X",
        "USDINR=X",
        "USDQAR=X",
        "USDSAR=X",
        "USDAED=X",
        "CL=F",
        "RB=F",
        "HO=F",
        "ZC=F",
        "ZW=F",
        "ZS=F",
        "KC=F",
        "CC=F",
        "SB=F",
        "CT=F",
        "BTC-USD",
        "ETH-USD",
        "BNB-USD",
        "XRP-USD",
        "ADA-USD",
        "DOGE-USD",
        "AVAX-USD",
        "TRX-USD",
        "LINK-USD",
        "SOL-USD"
    ];

    public async Task<MarketProviderPayloadDto> FetchAsync(CancellationToken cancellationToken = default)
    {
        var tcmbTask = TryFetchTcmbRatesAsync(cancellationToken);
        var yahooTask = FetchYahooQuotesAsync(cancellationToken);
        var localGoldTask = settings.Value.PreferLocalGoldRates
            ? TryFetchLocalGoldRatesAsync(cancellationToken)
            : Task.FromResult<IReadOnlyList<LocalGoldRate>>([]);
        await Task.WhenAll(tcmbTask, yahooTask, localGoldTask);

        if (yahooTask.Result.Count == 0 && tcmbTask.Result.Count == 0 && localGoldTask.Result.Count == 0)
        {
            throw new InvalidOperationException("Canlı piyasa endpointlerinden geçerli veri alınamadı.");
        }

        var quotes = BuildQuotes(yahooTask.Result, tcmbTask.Result, localGoldTask.Result);
        if (quotes.Count == 0)
        {
            throw new InvalidOperationException("Canlı piyasa endpointlerinden geçerli veri alınamadı.");
        }

        var usesLocalGold = localGoldTask.Result.Count > 0
            && quotes.Any(quote => string.Equals(quote.Symbol, "GRAM_ALTIN", StringComparison.OrdinalIgnoreCase));

        return new MarketProviderPayloadDto
        {
            ProviderKey = ProviderKey,
            ProviderDisplayName = usesLocalGold ? "Yerel Altin + Yahoo Finance + TCMB" : "Yahoo Finance + TCMB",
            IsFallback = false,
            Note = usesLocalGold
                ? "Turkiye altin alis/satış referansi, TCMB döviz ve Yahoo piyasa akışı"
                : "Gercek zamanli piyasa akışı",
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

    private async Task<IReadOnlyList<LocalGoldRate>> TryFetchLocalGoldRatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, settings.Value.LocalGoldRatesUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", settings.Value.UserAgent);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("Rates", out var ratesNode))
            {
                return [];
            }

            var rates = new List<LocalGoldRate>();
            AddLocalGoldRate(rates, ratesNode, "GRA", "GRAM_ALTIN", "Gram Altin", MarketCategory.Gold, "gram", 2);
            AddLocalGoldRate(rates, ratesNode, "CEYREKALTIN", "CEYREK_ALTIN", "Ceyrek Altin", MarketCategory.Gold, "adet", 3);
            AddLocalGoldRate(rates, ratesNode, "YARIMALTIN", "YARIM_ALTIN", "Yarim Altin", MarketCategory.Gold, "adet", 4);
            AddLocalGoldRate(rates, ratesNode, "TAMALTIN", "TAM_ALTIN", "Tam Altin", MarketCategory.Gold, "adet", 5);
            AddLocalGoldRate(rates, ratesNode, "CUMHURIYETALTINI", "CUMHURIYET_ALTIN", "Cumhuriyet Altini", MarketCategory.Gold, "adet", 6);
            AddLocalGoldRate(rates, ratesNode, "14AYARALTIN", "ALTIN_14K", "14 Ayar Altin", MarketCategory.Gold, "gram", 7);
            AddLocalGoldRate(rates, ratesNode, "18AYARALTIN", "ALTIN_18K", "18 Ayar Altin", MarketCategory.Gold, "gram", 8);
            AddLocalGoldRate(rates, ratesNode, "YIA", "ALTIN_22K", "22 Ayar Bilezik", MarketCategory.Gold, "gram", 9);
            AddLocalGoldRate(rates, ratesNode, "HAS", "HAS_ALTIN", "Has Altin", MarketCategory.Gold, "gram", 10);
            AddLocalGoldRate(rates, ratesNode, "ATAALTIN", "ATA_ALTIN", "Ata Altin", MarketCategory.Gold, "adet", 11);
            AddLocalGoldRate(rates, ratesNode, "RESATALTIN", "RESAT_ALTIN", "Resat Altin", MarketCategory.Gold, "adet", 12);
            AddLocalGoldRate(rates, ratesNode, "GREMSEALTIN", "GREMSE_ALTIN", "Gremse Altin", MarketCategory.Gold, "adet", 13);
            AddLocalGoldRate(rates, ratesNode, "GUMUS", "GUMUS", "Gumus", MarketCategory.Metal, "gram", 60);
            AddLocalGoldRate(rates, ratesNode, "GPL", "PLATIN_GRAM", "Platin Gram", MarketCategory.Metal, "gram", 61);
            AddLocalGoldRate(rates, ratesNode, "PAL", "PALADYUM_GRAM", "Paladyum Gram", MarketCategory.Metal, "gram", 62);

            return rates;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Local gold rates endpoint is unavailable.");
            return [];
        }
    }

    private List<MarketProviderQuoteDto> BuildQuotes(
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates,
        IReadOnlyList<LocalGoldRate> localGoldRates)
    {
        var quotes = new List<MarketProviderQuoteDto>();

        AddCurrencyQuotes(quotes, yahooQuotes, tcmbRates);
        var tryPerUsd = ResolveTryPerUsd(yahooQuotes, tcmbRates);
        var addedLocalGold = AddLocalTryRates(quotes, localGoldRates, tryPerUsd);
        AddGoldQuotes(quotes, yahooQuotes, addedLocalGold);
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
        AddCurrencyQuote(quotes, "TRY", "Turk Lirasi", 10, ResolveTryQuoteUsdPerTry(yahooQuotes, tcmbRates));
        quotes.Add(CreateConstantCurrencyQuote("USD", "Amerikan Dolari", 11));
        AddCurrencyQuote(quotes, "EUR", "Euro", 12, ResolveDirectOrTcmb("EUR", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "GBP", "Sterlin", 13, ResolveDirectOrTcmb("GBP", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "CHF", "Isvicre Frangi", 14, ResolveInverseOrTcmb("CHF", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "JPY", "Japon Yeni", 15, ResolveInverseOrTcmb("JPY", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "SAR", "Suudi Riyali", 16, ResolveInverseOrTcmb("SAR", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "AED", "BAE Dirhemi", 17, ResolveInverseOrTcmb("AED", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "CAD", "Kanada Dolari", 18, ResolveInverseOrTcmb("CAD", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "AUD", "Avustralya Dolari", 19, ResolveDirectOrTcmb("AUD", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "NZD", "Yeni Zelanda Dolari", 20, ResolveDirectOrTcmb("NZD", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "DKK", "Danimarka Kronu", 21, ResolveInverseOrTcmb("DKK", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "SEK", "Isvec Kronu", 22, ResolveInverseOrTcmb("SEK", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "NOK", "Norvec Kronu", 23, ResolveInverseOrTcmb("NOK", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "ZAR", "Guney Afrika Randi", 24, ResolveInverseOrTcmb("ZAR", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "CNY", "Cin Yuani", 25, ResolveInverseOrTcmb("CNY", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "HKD", "Hong Kong Dolari", 26, ResolveInverseOrTcmb("HKD", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "SGD", "Singapur Dolari", 27, ResolveInverseOrTcmb("SGD", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "INR", "Hindistan Rupisi", 28, ResolveInverseOrTcmb("INR", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "MXN", "Meksika Pesosu", 29, ResolveInverseOrTcmb("MXN", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "QAR", "Katar Riyali", 30, ResolveInverseOrTcmb("QAR", yahooQuotes, tcmbRates));
        AddCurrencyQuote(quotes, "KWD", "Kuveyt Dinari", 31, TryBuildFromTcmb("KWD", tcmbRates));
        AddCurrencyQuote(quotes, "RUB", "Rus Rublesi", 32, TryBuildFromTcmb("RUB", tcmbRates));
        AddCurrencyQuote(quotes, "PKR", "Pakistan Rupisi", 33, TryBuildFromTcmb("PKR", tcmbRates));
        AddCurrencyQuote(quotes, "KRW", "Guney Kore Wonu", 34, TryBuildFromTcmb("KRW", tcmbRates));
    }

    private void AddGoldQuotes(
        ICollection<MarketProviderQuoteDto> quotes,
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        bool addedLocalGold)
    {
        if (!yahooQuotes.TryGetValue("GC=F", out var ounceGold))
        {
            return;
        }

        var pureGram = Scale(ounceGold, 1m / MarketCatalog.TroyOunceInGrams);
        var gramAltin = Scale(ounceGold, 0.995m / MarketCatalog.TroyOunceInGrams);

        quotes.Add(ToProviderQuote("ONS_ALTIN", "Ons Altin", MarketCategory.Gold, "ons", 1, ounceGold, "COMEX Gold Futures", "live_market"));
        if (!addedLocalGold)
        {
            quotes.Add(ToProviderQuote("GRAM_ALTIN", "Gram Altin", MarketCategory.Gold, "gram", 2, gramAltin, "COMEX Gold Futures x saflik", "derived_formula", "ONS_ALTIN / 31.1034768 * 0.995"));
            quotes.Add(ToProviderQuote("CEYREK_ALTIN", "Ceyrek Altin", MarketCategory.Gold, "adet", 3, Scale(ounceGold, MarketCatalog.QuarterGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi", "derived_formula", "ONS_ALTIN / 31.1034768 * quarterGoldGramFactor * 22/24"));
            quotes.Add(ToProviderQuote("YARIM_ALTIN", "Yarim Altin", MarketCategory.Gold, "adet", 4, Scale(ounceGold, MarketCatalog.HalfGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi", "derived_formula", "CEYREK_ALTIN * 2"));
            quotes.Add(ToProviderQuote("TAM_ALTIN", "Tam Altin", MarketCategory.Gold, "adet", 5, Scale(ounceGold, MarketCatalog.FullGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi", "derived_formula", "YARIM_ALTIN * 2"));
            quotes.Add(ToProviderQuote("CUMHURIYET_ALTIN", "Cumhuriyet Altini", MarketCategory.Gold, "adet", 6, Scale(ounceGold, MarketCatalog.RepublicGoldWeightGram * (22m / 24m) / MarketCatalog.TroyOunceInGrams), "Spot altin bazli ziynet hesaplamasi", "derived_formula", "ONS_ALTIN / 31.1034768 * republicGoldGramFactor * 22/24"));
            quotes.Add(ToProviderQuote("ALTIN_14K", "14 Ayar Altin", MarketCategory.Gold, "gram", 7, Scale(pureGram, 14m / 24m), "Spot altin saflik katsayisi", "derived_formula", "HAS_ALTIN * 14/24"));
            quotes.Add(ToProviderQuote("ALTIN_18K", "18 Ayar Altin", MarketCategory.Gold, "gram", 8, Scale(pureGram, 18m / 24m), "Spot altin saflik katsayisi", "derived_formula", "HAS_ALTIN * 18/24"));
            quotes.Add(ToProviderQuote("ALTIN_22K", "22 Ayar Altin", MarketCategory.Gold, "gram", 9, Scale(pureGram, 22m / 24m), "Spot altin saflik katsayisi", "derived_formula", "HAS_ALTIN * 22/24"));
            quotes.Add(ToProviderQuote("HAS_ALTIN", "Has Altin", MarketCategory.Gold, "gram", 10, Scale(pureGram, 0.999m), "Spot altin saflik katsayisi", "derived_formula", "ONS_ALTIN / 31.1034768 * 0.999"));
        }
    }

    private void AddMetalAndCommodityQuotes(ICollection<MarketProviderQuoteDto> quotes, IReadOnlyDictionary<string, SourceQuote> yahooQuotes)
    {
        if (yahooQuotes.TryGetValue("SI=F", out var silver) && !quotes.Any(x => x.Symbol == "GUMUS_ONS"))
        {
            quotes.Add(ToProviderQuote("GUMUS_ONS", "Gumus Ons", MarketCategory.Metal, "ons", 63, silver, "COMEX Silver Futures"));
        }

        if (yahooQuotes.TryGetValue("PL=F", out var platinum))
        {
            quotes.Add(ToProviderQuote("PLATINUM", "Platin Ons", MarketCategory.Metal, "ons", 64, platinum, "NYMEX Platinum Futures"));
        }

        if (yahooQuotes.TryGetValue("PA=F", out var palladium))
        {
            quotes.Add(ToProviderQuote("PALLADIUM", "Paladyum Ons", MarketCategory.Metal, "ons", 65, palladium, "NYMEX Palladium Futures"));
        }

        if (yahooQuotes.TryGetValue("HG=F", out var copper))
        {
            quotes.Add(ToProviderQuote("COPPER", "Bakir", MarketCategory.Metal, "libre", 66, copper, "COMEX Copper Futures"));
        }

        if (yahooQuotes.TryGetValue("BZ=F", out var brent))
        {
            quotes.Add(ToProviderQuote("BRENT", "Brent Petrol", MarketCategory.Commodity, "varil", 80, brent, "ICE Brent Crude Futures"));
        }

        if (yahooQuotes.TryGetValue("CL=F", out var crudeOil))
        {
            quotes.Add(ToProviderQuote("WTI", "WTI Petrol", MarketCategory.Commodity, "varil", 81, crudeOil, "NYMEX WTI Crude Futures"));
        }

        if (yahooQuotes.TryGetValue("NG=F", out var naturalGas))
        {
            quotes.Add(ToProviderQuote("NATGAS", "Dogalgaz", MarketCategory.Commodity, "mmbtu", 82, naturalGas, "NYMEX Natural Gas Futures"));
        }

        AddYahooQuote(quotes, yahooQuotes, "RB=F", "GASOLINE", "Benzin Vadeli", MarketCategory.Commodity, "galon", 83, "NYMEX RBOB Gasoline Futures");
        AddYahooQuote(quotes, yahooQuotes, "HO=F", "HEATING_OIL", "Kalorifer Yagi", MarketCategory.Commodity, "galon", 84, "NYMEX Heating Oil Futures");
        AddYahooQuote(quotes, yahooQuotes, "ZC=F", "CORN", "Misir", MarketCategory.Commodity, "bushel", 85, "CBOT Corn Futures");
        AddYahooQuote(quotes, yahooQuotes, "ZW=F", "WHEAT", "Bugday", MarketCategory.Commodity, "bushel", 86, "CBOT Wheat Futures");
        AddYahooQuote(quotes, yahooQuotes, "ZS=F", "SOYBEAN", "Soya Fasulyesi", MarketCategory.Commodity, "bushel", 87, "CBOT Soybean Futures");
        AddYahooQuote(quotes, yahooQuotes, "KC=F", "COFFEE", "Kahve", MarketCategory.Commodity, "libre", 88, "ICE Coffee Futures");
        AddYahooQuote(quotes, yahooQuotes, "CC=F", "COCOA", "Kakao", MarketCategory.Commodity, "ton", 89, "ICE Cocoa Futures");
        AddYahooQuote(quotes, yahooQuotes, "SB=F", "SUGAR", "Seker", MarketCategory.Commodity, "libre", 90, "ICE Sugar Futures");
        AddYahooQuote(quotes, yahooQuotes, "CT=F", "COTTON", "Pamuk", MarketCategory.Commodity, "libre", 91, "ICE Cotton Futures");
    }

    private void AddCryptoQuotes(ICollection<MarketProviderQuoteDto> quotes, IReadOnlyDictionary<string, SourceQuote> yahooQuotes)
    {
        AddYahooQuote(quotes, yahooQuotes, "BTC-USD", "BTC", "Bitcoin", MarketCategory.Crypto, "adet", 100, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "ETH-USD", "ETH", "Ethereum", MarketCategory.Crypto, "adet", 101, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "BNB-USD", "BNB", "BNB", MarketCategory.Crypto, "adet", 102, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "SOL-USD", "SOL", "Solana", MarketCategory.Crypto, "adet", 103, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "XRP-USD", "XRP", "XRP", MarketCategory.Crypto, "adet", 104, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "ADA-USD", "ADA", "Cardano", MarketCategory.Crypto, "adet", 105, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "DOGE-USD", "DOGE", "Dogecoin", MarketCategory.Crypto, "adet", 106, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "AVAX-USD", "AVAX", "Avalanche", MarketCategory.Crypto, "adet", 107, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "TRX-USD", "TRX", "TRON", MarketCategory.Crypto, "adet", 108, "Yahoo Finance Crypto");
        AddYahooQuote(quotes, yahooQuotes, "LINK-USD", "LINK", "Chainlink", MarketCategory.Crypto, "adet", 109, "Yahoo Finance Crypto");
    }

    private static void AddYahooQuote(
        ICollection<MarketProviderQuoteDto> quotes,
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        string yahooSymbol,
        string symbol,
        string displayName,
        MarketCategory category,
        string unitLabel,
        int sortOrder,
        string note,
        string sourceType = "live_market",
        string? calculationBasis = null)
    {
        if (yahooQuotes.TryGetValue(yahooSymbol, out var source))
        {
            quotes.Add(ToProviderQuote(symbol, displayName, category, unitLabel, sortOrder, source, note, sourceType, calculationBasis));
        }
    }

    private static void AddCurrencyQuote(
        ICollection<MarketProviderQuoteDto> quotes,
        string symbol,
        string displayName,
        int sortOrder,
        SourceQuote? source)
    {
        if (source is not null)
        {
            quotes.Add(ToProviderQuote(symbol, displayName, MarketCategory.Currency, "1 birim", sortOrder, source, "Yahoo Finance / TCMB", "live_market"));
        }
    }

    private static bool AddLocalTryRates(
        ICollection<MarketProviderQuoteDto> quotes,
        IReadOnlyList<LocalGoldRate> localRates,
        decimal? tryPerUsd)
    {
        if (tryPerUsd is null || tryPerUsd <= 0 || localRates.Count == 0)
        {
            return false;
        }

        var addedGold = false;
        foreach (var rate in localRates.Where(x => x.SellTry > 0))
        {
            quotes.Add(ToProviderQuoteFromTry(rate, tryPerUsd.Value));
            addedGold = addedGold || rate.Category == MarketCategory.Gold;
        }

        return addedGold;
    }

    private static void AddLocalGoldRate(
        ICollection<LocalGoldRate> rates,
        JsonElement ratesNode,
        string sourceKey,
        string symbol,
        string displayName,
        MarketCategory category,
        string unitLabel,
        int sortOrder)
    {
        if (!ratesNode.TryGetProperty(sourceKey, out var sourceNode))
        {
            return;
        }

        var sellTry = TryGetDecimal(sourceNode, "Selling") ?? 0m;
        var buyTry = TryGetDecimal(sourceNode, "Buying") ?? sellTry;
        var changePercent = TryGetDecimal(sourceNode, "Change");
        if (sellTry <= 0)
        {
            return;
        }

        rates.Add(new LocalGoldRate(
            symbol,
            displayName,
            category,
            unitLabel,
            sortOrder,
            buyTry <= 0 ? sellTry : buyTry,
            sellTry,
            changePercent));
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
            Note = "ABD Dolar referansi",
            SourceType = "live_market"
        };

    private static MarketProviderQuoteDto ToProviderQuote(
        string symbol,
        string displayName,
        MarketCategory category,
        string unitLabel,
        int sortOrder,
        SourceQuote source,
        string note,
        string sourceType = "live_market",
        string? calculationBasis = null)
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
            Note = note,
            SourceType = sourceType,
            CalculationBasis = calculationBasis
        };

    private static MarketProviderQuoteDto ToProviderQuoteFromTry(LocalGoldRate rate, decimal tryPerUsd)
    {
        var currentUsd = rate.SellTry / tryPerUsd;
        var buyUsd = rate.BuyTry / tryPerUsd;
        var previousUsd = rate.ChangePercent is { } change && change > -99m
            ? currentUsd / (1m + (change / 100m))
            : buyUsd;

        return new MarketProviderQuoteDto
        {
            Symbol = rate.Symbol,
            DisplayName = rate.DisplayName,
            Category = rate.Category,
            UnitLabel = rate.UnitLabel,
            NativeCurrency = "TRY",
            PriceInUsd = RoundPrice(currentUsd),
            Price24hAgoInUsd = RoundPrice(previousUsd),
            High24hInUsd = RoundPrice(Math.Max(currentUsd, previousUsd)),
            Low24hInUsd = RoundPrice(Math.Min(currentUsd, previousUsd)),
            SortOrder = rate.SortOrder,
            Note = "Turkiye yerel alis/satış referansi",
            SourceType = "local_reference"
        };
    }

    private static SourceQuote? ResolveDirectOrTcmb(
        string currencyCode,
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        var yahooSymbol = currencyCode switch
        {
            "EUR" => "EURUSD=X",
            "GBP" => "GBPUSD=X",
            "AUD" => "AUDUSD=X",
            "NZD" => "NZDUSD=X",
            _ => null
        };

        if (yahooSymbol is not null && yahooQuotes.TryGetValue(yahooSymbol, out var direct))
        {
            return direct;
        }

        return TryBuildFromTcmb(currencyCode, tcmbRates);
    }

    private static SourceQuote? ResolveTryQuoteUsdPerTry(
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        if (yahooQuotes.TryGetValue("USDTRY=X", out var usdTry))
        {
            return Invert(usdTry);
        }

        return TryBuildFromTcmb("TRY", tcmbRates);
    }

    private static decimal? ResolveTryPerUsd(
        IReadOnlyDictionary<string, SourceQuote> yahooQuotes,
        IReadOnlyDictionary<string, decimal> tcmbRates)
    {
        if (yahooQuotes.TryGetValue("USDTRY=X", out var usdTry) && usdTry.Current > 0)
        {
            return usdTry.Current;
        }

        return tcmbRates.TryGetValue("USD", out var tcmbUsd) && tcmbUsd > 0
            ? tcmbUsd
            : null;
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
            "MXN" => "USDMXN=X",
            "NOK" => "USDNOK=X",
            "SEK" => "USDSEK=X",
            "DKK" => "USDDKK=X",
            "ZAR" => "USDZAR=X",
            "SGD" => "USDSGD=X",
            "HKD" => "USDHKD=X",
            "CNY" => "USDCNY=X",
            "INR" => "USDINR=X",
            "QAR" => "USDQAR=X",
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

    private sealed record LocalGoldRate(
        string Symbol,
        string DisplayName,
        MarketCategory Category,
        string UnitLabel,
        int SortOrder,
        decimal BuyTry,
        decimal SellTry,
        decimal? ChangePercent);
}
