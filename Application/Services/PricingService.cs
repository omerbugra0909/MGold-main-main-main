using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Domain.Entities;
using MGold.Domain.Enums;
using MGold.Infrastructure.Repositories.Interfaces;

namespace MGold.Application.Services;

public class PricingService(IGoldPriceRepository goldPriceRepository) : IPricingService
{
    public async Task<ProductPricePreviewDto> CalculateCurrentPriceAsync(Product product, CancellationToken cancellationToken = default)
    {
        // Pricing is centralized here so controllers/services don't duplicate formula rules.
        var goldPrice = await GetLatestGoldPriceForProductAsync(product, cancellationToken);
        var materialCost = CalculateMaterialCost(product, goldPrice);
        var laborPercentageCost = materialCost * product.LaborCostPercentage / 100m;
        var purchaseCost = materialCost + product.LaborCost + laborPercentageCost + product.AdditionalCost;
        var recommendedSalePrice = product.SalePrice > 0
            ? product.SalePrice
            : purchaseCost * (1 + product.ProfitMarginPercentage / 100m);

        return new ProductPricePreviewDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            ProductType = product.Type,
            Weight = product.Weight,
            PurityRate = product.PurityRate,
            GoldPricePerGram = goldPrice,
            MaterialCost = DecimalRound(materialCost),
            LaborCost = DecimalRound(product.LaborCost + laborPercentageCost),
            LaborCostPercentage = product.LaborCostPercentage,
            AdditionalCost = DecimalRound(product.AdditionalCost),
            PurchaseCost = DecimalRound(purchaseCost),
            ProfitMarginPercentage = product.ProfitMarginPercentage,
            RecommendedSalePrice = DecimalRound(recommendedSalePrice),
            UsesLiveGoldPrice = product.Type == ProductType.Gold
        };
    }

    public async Task<ProductPricingSnapshot> CreateTransactionSnapshotAsync(Product product, TransactionType transactionType, decimal? manualUnitPrice, bool useLiveCalculatedPrice, int quantity, CancellationToken cancellationToken = default)
    {
        var preview = await CalculateCurrentPriceAsync(product, cancellationToken);
        var purchaseCost = preview.PurchaseCost;
        var salePrice = preview.RecommendedSalePrice;

        // We persist the calculated market context so historical P/L stays stable after price changes.
        var appliedUnitPrice = useLiveCalculatedPrice || !manualUnitPrice.HasValue || manualUnitPrice <= 0
            ? transactionType == TransactionType.Buy ? purchaseCost : salePrice
            : manualUnitPrice.Value;

        var totalPrice = appliedUnitPrice * quantity;
        var totalCost = purchaseCost * quantity;
        var profitOrLoss = transactionType == TransactionType.Sell
            ? totalPrice - totalCost
            : 0m;

        return new ProductPricingSnapshot
        {
            GoldPricePerGram = preview.GoldPricePerGram,
            ProductWeight = product.Weight,
            PurityRate = product.PurityRate,
            MaterialCost = preview.MaterialCost,
            LaborCost = preview.LaborCost,
            LaborCostPercentage = product.LaborCostPercentage,
            AdditionalCost = product.AdditionalCost,
            ProfitMarginPercentage = product.ProfitMarginPercentage,
            CalculatedPurchasePrice = purchaseCost,
            CalculatedSalePrice = salePrice,
            AppliedUnitPrice = DecimalRound(appliedUnitPrice),
            TotalPrice = DecimalRound(totalPrice),
            TotalCost = DecimalRound(totalCost),
            ProfitOrLoss = DecimalRound(profitOrLoss)
        };
    }

    private async Task<decimal> GetLatestGoldPriceForProductAsync(Product product, CancellationToken cancellationToken)
    {
        if (product.Type != ProductType.Gold)
        {
            return 0m;
        }

        var goldPrice = await goldPriceRepository.GetLatestActiveAsync(cancellationToken)
            ?? throw new BusinessRuleException("Active gold price record is required to price gold products.");

        return goldPrice.PricePerGram;
    }

    private static decimal CalculateMaterialCost(Product product, decimal currentGoldPrice)
        => product.Type == ProductType.Gold
            ? product.Weight * product.PurityRate * currentGoldPrice
            : product.PurchasePrice;

    private static decimal DecimalRound(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
