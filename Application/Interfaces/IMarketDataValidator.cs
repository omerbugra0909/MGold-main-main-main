using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IMarketDataValidator
{
    MarketProviderQuoteDto NormalizeProviderQuote(MarketProviderPayloadDto payload, MarketProviderQuoteDto quote);
    MarketQuoteDto NormalizeMappedQuote(MarketQuoteDto quote);
    void WarnIfGoldOunceMismatch(MarketProviderPayloadDto payload);
}
