using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Constants;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Data;
using MGold.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MGold.Application.Services;

public class TransactionService(
    ITransactionRepository transactionRepository,
    IProductRepository productRepository,
    ICustomerRepository customerRepository,
    IPricingService pricingService,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService,
    AppDbContext context,
    IUnitOfWork unitOfWork,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<IReadOnlyList<TransactionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var items = await BuildTransactionQuery()
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<TransactionDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var transaction = await BuildTransactionQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Transaction with id {id} was not found.");

        return transaction.ToDto();
    }

    public async Task<TransactionDto> CreateAsync(CreateTransactionDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        ValidateTransactionInput(dto);

        var product = await GetProductAsync(dto.ProductId, cancellationToken);
        var customer = await GetCustomerIfExistsAsync(dto.CustomerId, cancellationToken);
        accessControlService.EnsureSameCompany(product.CompanyId);
        if (customer is not null)
        {
            accessControlService.EnsureSameCompany(customer.CompanyId);
        }
        // Snapshot first, then stock update, so transaction data reflects the exact market state used for the operation.
        var pricingSnapshot = await pricingService.CreateTransactionSnapshotAsync(
            product,
            dto.Type,
            dto.UnitPrice,
            dto.UseLiveCalculatedPrice,
            dto.Quantity,
            cancellationToken);

        ApplyStockChange(product, dto.Type, dto.Quantity);

        var transaction = new Transaction
        {
            CompanyId = product.CompanyId ?? customer?.CompanyId ?? currentUserService.CompanyId,
            ProductId = product.Id,
            CustomerId = customer?.Id,
            Type = dto.Type,
            Quantity = dto.Quantity,
            UnitPrice = pricingSnapshot.AppliedUnitPrice,
            TotalPrice = pricingSnapshot.TotalPrice,
            GoldPricePerGramSnapshot = pricingSnapshot.GoldPricePerGram,
            ProductWeightSnapshot = pricingSnapshot.ProductWeight,
            PurityRateSnapshot = pricingSnapshot.PurityRate,
            MaterialCostSnapshot = pricingSnapshot.MaterialCost,
            LaborCostSnapshot = pricingSnapshot.LaborCost,
            LaborCostPercentageSnapshot = pricingSnapshot.LaborCostPercentage,
            AdditionalCostSnapshot = pricingSnapshot.AdditionalCost,
            ProfitMarginPercentageSnapshot = pricingSnapshot.ProfitMarginPercentage,
            CalculatedPurchasePriceSnapshot = pricingSnapshot.CalculatedPurchasePrice,
            CalculatedSalePriceSnapshot = pricingSnapshot.CalculatedSalePrice,
            TotalCostSnapshot = pricingSnapshot.TotalCost,
            ProfitOrLoss = pricingSnapshot.ProfitOrLoss,
            Date = dto.Date?.ToUniversalTime() ?? DateTime.UtcNow
        };

        await transactionRepository.AddAsync(transaction, cancellationToken);
        productRepository.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Transaction {TransactionId} created for product {ProductId}.", transaction.Id, product.Id);

        var saved = await transactionRepository.GetByIdWithDetailsAsync(transaction.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved transaction could not be reloaded.");

        return saved.ToDto();
    }

    public async Task<TransactionDto> UpdateAsync(int id, UpdateTransactionDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanWriteOperationalData();
        ValidateTransactionInput(dto);

        var transaction = await transactionRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Transaction with id {id} was not found.");
        accessControlService.EnsureSameCompany(transaction.CompanyId);

        var originalProduct = await GetProductAsync(transaction.ProductId, cancellationToken);
        ReverseStockChange(originalProduct, transaction.Type, transaction.Quantity);

        var targetProduct = dto.ProductId == originalProduct.Id
            ? originalProduct
            : await GetProductAsync(dto.ProductId, cancellationToken);

        var customer = await GetCustomerIfExistsAsync(dto.CustomerId, cancellationToken);
        accessControlService.EnsureSameCompany(targetProduct.CompanyId);
        if (customer is not null)
        {
            accessControlService.EnsureSameCompany(customer.CompanyId);
        }
        var pricingSnapshot = await pricingService.CreateTransactionSnapshotAsync(
            targetProduct,
            dto.Type,
            dto.UnitPrice,
            dto.UseLiveCalculatedPrice,
            dto.Quantity,
            cancellationToken);

        ApplyStockChange(targetProduct, dto.Type, dto.Quantity);

        transaction.ProductId = targetProduct.Id;
        transaction.CompanyId = targetProduct.CompanyId ?? customer?.CompanyId ?? currentUserService.CompanyId;
        transaction.CustomerId = customer?.Id;
        transaction.Type = dto.Type;
        transaction.Quantity = dto.Quantity;
        transaction.UnitPrice = pricingSnapshot.AppliedUnitPrice;
        transaction.TotalPrice = pricingSnapshot.TotalPrice;
        transaction.GoldPricePerGramSnapshot = pricingSnapshot.GoldPricePerGram;
        transaction.ProductWeightSnapshot = pricingSnapshot.ProductWeight;
        transaction.PurityRateSnapshot = pricingSnapshot.PurityRate;
        transaction.MaterialCostSnapshot = pricingSnapshot.MaterialCost;
        transaction.LaborCostSnapshot = pricingSnapshot.LaborCost;
        transaction.LaborCostPercentageSnapshot = pricingSnapshot.LaborCostPercentage;
        transaction.AdditionalCostSnapshot = pricingSnapshot.AdditionalCost;
        transaction.ProfitMarginPercentageSnapshot = pricingSnapshot.ProfitMarginPercentage;
        transaction.CalculatedPurchasePriceSnapshot = pricingSnapshot.CalculatedPurchasePrice;
        transaction.CalculatedSalePriceSnapshot = pricingSnapshot.CalculatedSalePrice;
        transaction.TotalCostSnapshot = pricingSnapshot.TotalCost;
        transaction.ProfitOrLoss = pricingSnapshot.ProfitOrLoss;
        transaction.Date = dto.Date?.ToUniversalTime() ?? transaction.Date;

        productRepository.Update(originalProduct);
        if (targetProduct.Id != originalProduct.Id)
        {
            productRepository.Update(targetProduct);
        }

        transactionRepository.Update(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Transaction {TransactionId} updated.", transaction.Id);

        var saved = await transactionRepository.GetByIdWithDetailsAsync(transaction.Id, cancellationToken)
            ?? throw new InvalidOperationException("Updated transaction could not be reloaded.");

        return saved.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanDeleteData();

        var transaction = await transactionRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Transaction with id {id} was not found.");
        accessControlService.EnsureSameCompany(transaction.CompanyId);

        // Stock is reversed from the persisted transaction record instead of recalculating from current market inputs.
        var product = await GetProductAsync(transaction.ProductId, cancellationToken);
        ReverseStockChange(product, transaction.Type, transaction.Quantity);

        productRepository.Update(product);
        transactionRepository.Remove(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Transaction {TransactionId} deleted.", transaction.Id);
    }

    private async Task<Product> GetProductAsync(int productId, CancellationToken cancellationToken)
        => await productRepository.GetByIdAsync(productId, cancellationToken)
           ?? throw new KeyNotFoundException($"Product with id {productId} was not found.");

    private async Task<Customer?> GetCustomerIfExistsAsync(int? customerId, CancellationToken cancellationToken)
    {
        if (!customerId.HasValue)
        {
            return null;
        }

        return await customerRepository.GetByIdAsync(customerId.Value, cancellationToken)
               ?? throw new KeyNotFoundException($"Customer with id {customerId.Value} was not found.");
    }

    private IQueryable<Transaction> BuildTransactionQuery()
    {
        var query = context.Transactions
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .AsQueryable();

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        return query;
    }

    private static void ValidateTransactionInput(CreateTransactionDto dto)
    {
        if (dto.ProductId <= 0)
        {
            throw new BusinessRuleException("Product is required.");
        }

        if (dto.CustomerId is <= 0)
        {
            throw new BusinessRuleException("Customer id is invalid.");
        }

        if (!Enum.IsDefined(dto.Type))
        {
            throw new BusinessRuleException("Transaction type is invalid.");
        }

        if (dto.Quantity <= 0)
        {
            throw new BusinessRuleException("Transaction quantity must be greater than zero.");
        }

        if (dto.UnitPrice is < 0)
        {
            throw new BusinessRuleException("Unit price cannot be negative.");
        }
    }

    private static void ApplyStockChange(Product product, TransactionType type, int quantity)
    {
        if (type == TransactionType.Buy)
        {
            product.StockQuantity += quantity;
            return;
        }

        if (product.StockQuantity < quantity)
        {
            throw new BusinessRuleException($"Insufficient stock for product '{product.Name}'. Current stock: {product.StockQuantity}.");
        }

        product.StockQuantity -= quantity;
    }

    private static void ReverseStockChange(Product product, TransactionType type, int quantity)
    {
        if (type == TransactionType.Buy)
        {
            if (product.StockQuantity < quantity)
            {
                throw new BusinessRuleException($"Transaction reversal would make stock negative for product '{product.Name}'.");
            }

            product.StockQuantity -= quantity;
            return;
        }

        product.StockQuantity += quantity;
    }
}
