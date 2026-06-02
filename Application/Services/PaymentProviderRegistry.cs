using MGold.Application.Interfaces;

namespace MGold.Application.Services;

public class PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers) : IPaymentProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers = providers
        .GroupBy(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

    public IPaymentProvider Get(string? providerKey)
    {
        var key = string.IsNullOrWhiteSpace(providerKey) ? ManualPaymentProvider.Key : providerKey.Trim();
        if (_providers.TryGetValue(key, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"Payment provider '{key}' is not configured.");
    }
}
