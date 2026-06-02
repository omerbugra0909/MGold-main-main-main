using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IProductReviewService
{
    Task<IReadOnlyList<ProductReviewDto>> GetByProductAsync(int productId, bool includePending, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductReviewDto>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<ProductReviewDto> CreateAsync(CreateProductReviewDto dto, CancellationToken cancellationToken = default);
    Task<ProductReviewDto> ModerateAsync(int id, ModerateProductReviewDto dto, CancellationToken cancellationToken = default);
}
