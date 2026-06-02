using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IMarketDataProvider
{
    string ProviderKey { get; }
    Task<MarketProviderPayloadDto> FetchAsync(CancellationToken cancellationToken = default);
}
