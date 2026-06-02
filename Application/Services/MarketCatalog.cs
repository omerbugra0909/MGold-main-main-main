using MGold.Domain.Enums;

namespace MGold.Application.Services;

internal static class MarketCatalog
{
    private static readonly HashSet<string> HiddenSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRY"
    };

    public const decimal TroyOunceInGrams = 31.1034768m;
    public const decimal QuarterGoldWeightGram = 1.754m;
    public const decimal HalfGoldWeightGram = 3.508m;
    public const decimal FullGoldWeightGram = 7.016m;
    public const decimal RepublicGoldWeightGram = 7.216m;

    public static IReadOnlyList<string> SupportedCurrencies { get; } = ["TRY", "USD", "EUR", "GBP", "SAR", "AED", "CHF", "CAD", "JPY"];

    public static bool IsVisibleOnBoard(string symbol)
        => !HiddenSymbols.Contains(symbol);

    public static string ToCategoryLabel(MarketCategory category)
        => category switch
        {
            MarketCategory.Gold => "Altin",
            MarketCategory.Currency => "Doviz",
            MarketCategory.Metal => "Metaller",
            MarketCategory.Commodity => "Emtia",
            MarketCategory.Crypto => "Kripto",
            _ => "Market"
        };
}
