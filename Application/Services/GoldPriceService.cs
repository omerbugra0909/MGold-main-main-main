using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Entities;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Application.Services;

public class GoldPriceService(
    IGoldPriceRepository goldPriceRepository,
    IUnitOfWork unitOfWork,
    IAccessControlService accessControlService) : IGoldPriceService
{
    public async Task<IReadOnlyList<GoldPriceDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await goldPriceRepository.GetAllOrderedAsync(cancellationToken)).Select(x => x.ToDto()).ToList();

    public async Task<GoldPriceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var goldPrice = await goldPriceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Gold price with id {id} was not found.");

        return goldPrice.ToDto();
    }

    public async Task<GoldPriceDto> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var goldPrice = await goldPriceRepository.GetLatestActiveAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No active gold price record was found.");

        return goldPrice.ToDto();
    }

    public async Task<GoldPriceDto> CreateAsync(CreateGoldPriceDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureAdminOnly();
        ValidateGoldPrice(dto.PricePerGram);

        if (dto.IsActive)
        {
            await goldPriceRepository.DeactivateAllAsync(cancellationToken);
        }

        var entity = new GoldPrice
        {
            PricePerGram = dto.PricePerGram,
            EffectiveFrom = dto.EffectiveFrom?.ToUniversalTime() ?? DateTime.UtcNow,
            Source = string.IsNullOrWhiteSpace(dto.Source) ? null : dto.Source.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await goldPriceRepository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<GoldPriceDto> UpdateAsync(int id, UpdateGoldPriceDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureAdminOnly();
        ValidateGoldPrice(dto.PricePerGram);

        var entity = await goldPriceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Gold price with id {id} was not found.");

        if (dto.IsActive)
        {
            await goldPriceRepository.DeactivateAllAsync(cancellationToken);
        }
        else
        {
            var latest = await goldPriceRepository.GetLatestActiveAsync(cancellationToken);
            if (latest is not null && latest.Id == id)
            {
                throw new BusinessRuleException("At least one active gold price record must remain.");
            }
        }

        entity.PricePerGram = dto.PricePerGram;
        entity.EffectiveFrom = dto.EffectiveFrom?.ToUniversalTime() ?? entity.EffectiveFrom;
        entity.Source = string.IsNullOrWhiteSpace(dto.Source) ? null : dto.Source.Trim();
        entity.IsActive = dto.IsActive;

        goldPriceRepository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureAdminOnly();

        var entity = await goldPriceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Gold price with id {id} was not found.");

        if (entity.IsActive)
        {
            throw new BusinessRuleException("Active gold price records cannot be deleted. Mark another record as active first.");
        }

        goldPriceRepository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateGoldPrice(decimal pricePerGram)
    {
        if (pricePerGram <= 0)
        {
            throw new BusinessRuleException("Gold price per gram must be greater than zero.");
        }
    }
}
