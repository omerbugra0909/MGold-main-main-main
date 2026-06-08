using MGold.Application.DTOs;
using MGold.Application.Interfaces;

namespace MGold.Application.Services;

public class FallbackMarketDataProvider : IMarketDataProvider
{
    public string ProviderKey => "fallback-core";

    public Task<MarketProviderPayloadDto> FetchAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new MarketProviderPayloadDto
        {
            ProviderKey = ProviderKey,
            ProviderDisplayName = "Legacy Mock Provider",
            IsFallback = true,
            Note = "Mock veri devre disi. Sadece kayıtlı gerçek snapshot kullanilabilir.",
            FetchedAt = DateTime.UtcNow,
            Quotes = []
        });
}
