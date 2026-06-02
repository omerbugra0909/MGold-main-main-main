namespace MGold.Application.Interfaces;

public interface IPaymentProviderRegistry
{
    IPaymentProvider Get(string? providerKey);
}
