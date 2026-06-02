using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MGold.Application.Services;

public class ProductService(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IPricingService pricingService,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService,
    AppDbContext context) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(ProductFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        ValidateFilter(filter);

        var query = context.Products
            .AsNoTracking()
            .Include(x => x.Reviews)
            .AsQueryable();

        if (currentUserService.IsInRole(RoleConstants.Manager) || currentUserService.IsInRole(RoleConstants.Employee))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(search));
        }

        if (filter?.Type is not null)
        {
            query = query.Where(x => x.Type == filter.Type);
        }

        if (filter?.MinPrice is not null)
        {
            query = query.Where(x => x.SalePrice >= filter.MinPrice.Value);
        }

        if (filter?.MaxPrice is not null)
        {
            query = query.Where(x => x.SalePrice <= filter.MaxPrice.Value);
        }

        if (filter?.InStockOnly == true)
        {
            query = query.Where(x => x.StockQuantity > 0);
        }

        if (filter?.LowStockOnly == true)
        {
            query = query.Where(x => x.StockQuantity <= 5);
        }

        if (filter?.CreatedFrom is not null)
        {
            query = query.Where(x => x.CreatedAt >= filter.CreatedFrom.Value);
        }

        if (filter?.CreatedTo is not null)
        {
            query = query.Where(x => x.CreatedAt <= filter.CreatedTo.Value);
        }

        var products = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var items = new List<ProductDto>(products.Count);

        foreach (var product in products)
        {
            items.Add(await MapWithPricingAsync(product, cancellationToken));
        }

        return items;
    }

    public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var query = context.Products
            .AsNoTracking()
            .Include(x => x.Reviews)
            .AsQueryable();

        if (currentUserService.IsInRole(RoleConstants.Manager) || currentUserService.IsInRole(RoleConstants.Employee))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        var product = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with id {id} was not found.");

        return await MapWithPricingAsync(product, cancellationToken);
    }

    public async Task<ProductPricePreviewDto> GetPricePreviewAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with id {id} was not found.");

        return await pricingService.CalculateCurrentPriceAsync(product, cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        ValidateProductInput(dto);
        var normalizedName = NormalizeRequired(dto.Name, "Product name");

        var companyId = currentUserService.CompanyId;
        if (currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            companyId = dto.CompanyId;
            if (!companyId.HasValue)
            {
                throw new AuthorizationException("Sistem admini urun olustururken firma secmelidir.");
            }

            var companyExists = await context.Companies
                .AnyAsync(x => x.Id == companyId.Value && x.IsActive, cancellationToken);
            if (!companyExists)
            {
                throw new BusinessRuleException("Secilen firma bulunamadi veya pasif.");
            }
        }

        if (companyId is not int resolvedCompanyId)
        {
            throw new AuthorizationException("Urun olusturmak icin firma baglami zorunludur.");
        }

        var product = new Product
        {
            Name = normalizedName,
            Type = dto.Type,
            Weight = dto.Weight,
            PurityRate = dto.PurityRate,
            LaborCost = dto.LaborCost,
            LaborCostPercentage = dto.LaborCostPercentage,
            AdditionalCost = dto.AdditionalCost,
            ProfitMarginPercentage = dto.ProfitMarginPercentage,
            PurchasePrice = dto.PurchasePrice,
            SalePrice = dto.SalePrice,
            StockQuantity = dto.StockQuantity,
            CompanyId = resolvedCompanyId
        };

        await productRepository.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapWithPricingAsync(product, cancellationToken);
    }

    public async Task<ProductDto> UpdateAsync(int id, UpdateProductDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        ValidateProductInput(dto);
        var normalizedName = NormalizeRequired(dto.Name, "Product name");

        var product = await productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with id {id} was not found.");
        accessControlService.EnsureSameCompany(product.CompanyId);

        product.Name = normalizedName;
        product.Type = dto.Type;
        product.Weight = dto.Weight;
        product.PurityRate = dto.PurityRate;
        product.LaborCost = dto.LaborCost;
        product.LaborCostPercentage = dto.LaborCostPercentage;
        product.AdditionalCost = dto.AdditionalCost;
        product.ProfitMarginPercentage = dto.ProfitMarginPercentage;
        product.PurchasePrice = dto.PurchasePrice;
        product.SalePrice = dto.SalePrice;
        product.StockQuantity = dto.StockQuantity;

        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapWithPricingAsync(product, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanDeleteData();

        var product = await productRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with id {id} was not found.");
        accessControlService.EnsureSameCompany(product.CompanyId);

        if (await productRepository.HasTransactionsAsync(id, cancellationToken)
            || await context.OrderItems.AnyAsync(x => x.ProductId == id, cancellationToken))
        {
            throw new BusinessRuleException("Siparis veya islem gecmisi olan urun silinemez.");
        }

        productRepository.Remove(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductDto> MapWithPricingAsync(Product product, CancellationToken cancellationToken)
    {
        var dto = product.ToDto();

        try
        {
            dto.CurrentPricing = await pricingService.CalculateCurrentPriceAsync(product, cancellationToken);
        }
        catch (BusinessRuleException)
        {
            dto.CurrentPricing = null;
        }

        return dto;
    }

    private static void ValidateFilter(ProductFilterDto? filter)
    {
        if (filter is null)
        {
            return;
        }

        if (filter.Type.HasValue && !Enum.IsDefined(filter.Type.Value))
        {
            throw new BusinessRuleException("Product type is invalid.");
        }

        if (filter.MinPrice is < 0 || filter.MaxPrice is < 0)
        {
            throw new BusinessRuleException("Price filters cannot be negative.");
        }

        if (filter.MinPrice.HasValue && filter.MaxPrice.HasValue && filter.MinPrice > filter.MaxPrice)
        {
            throw new BusinessRuleException("Minimum price cannot be greater than maximum price.");
        }

        if (filter.CreatedFrom.HasValue && filter.CreatedTo.HasValue && filter.CreatedFrom > filter.CreatedTo)
        {
            throw new BusinessRuleException("Start date cannot be later than end date.");
        }
    }

    private static void ValidateProductInput(CreateProductDto dto)
    {
        if (!Enum.IsDefined(dto.Type))
        {
            throw new BusinessRuleException("Product type is invalid.");
        }

        if (dto.Weight < 0
            || dto.PurityRate < 0
            || dto.PurityRate > 1
            || dto.LaborCost < 0
            || dto.LaborCostPercentage < 0
            || dto.AdditionalCost < 0
            || dto.ProfitMarginPercentage < 0
            || dto.PurchasePrice < 0
            || dto.SalePrice < 0
            || dto.StockQuantity < 0)
        {
            throw new BusinessRuleException("Product numeric values cannot be negative and purity must be between 0 and 1.");
        }

        if (dto.Type == Domain.Enums.ProductType.Gold && (dto.Weight <= 0 || dto.PurityRate <= 0))
        {
            throw new BusinessRuleException("Gold products must have weight and purity greater than zero.");
        }
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessRuleException($"{fieldName} is required.");
        }

        return normalized;
    }
}
