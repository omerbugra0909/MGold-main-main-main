using MGold.Application.DTOs;

namespace MGold.Application.Interfaces;

public interface IGoldPriceService
{
    Task<IReadOnlyList<GoldPriceDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GoldPriceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<GoldPriceDto> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<GoldPriceDto> CreateAsync(CreateGoldPriceDto dto, CancellationToken cancellationToken = default);
    Task<GoldPriceDto> UpdateAsync(int id, UpdateGoldPriceDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
