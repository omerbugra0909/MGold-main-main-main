using MGold.Application.DTOs;
using MGold.Domain.Entities;
using MGold.Domain.Enums;

namespace MGold.Application.Interfaces;

public interface IPricingService
{
    Task<ProductPricePreviewDto> CalculateCurrentPriceAsync(Product product, CancellationToken cancellationToken = default);
    Task<ProductPricingSnapshot> CreateTransactionSnapshotAsync(Product product, TransactionType transactionType, decimal? manualUnitPrice, bool useLiveCalculatedPrice, int quantity, CancellationToken cancellationToken = default);
}
