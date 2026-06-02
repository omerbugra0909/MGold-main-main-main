using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Domain.Constants;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class ReportService(
    AppDbContext context,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService) : IReportService
{
    public async Task<ProfitLossSummaryDto> GetProfitLossSummaryAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var query = context.Transactions
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();
        var productsQuery = context.Products.AsNoTracking().AsQueryable();

        if (!currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            query = query.Where(x => x.CompanyId == currentUserService.CompanyId);
            productsQuery = productsQuery.Where(x => x.CompanyId == currentUserService.CompanyId);
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.Date >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(x => x.Date <= endDate.Value.ToUniversalTime());
        }

        var transactions = await query.ToListAsync(cancellationToken);

        var sales = transactions.Where(x => x.Type == Domain.Enums.TransactionType.Sell).ToList();
        var buys = transactions.Where(x => x.Type == Domain.Enums.TransactionType.Buy).ToList();

        var products = await productsQuery.ToListAsync(cancellationToken);
        var latestGoldPrice = await context.GoldPrices
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Current inventory valuation uses today's active gold price, while realized sales P/L uses transaction snapshots.
        decimal CalculateCurrentPurchaseCost(Domain.Entities.Product product)
        {
            var materialCost = product.Type == Domain.Enums.ProductType.Gold
                ? product.Weight * product.PurityRate * (latestGoldPrice?.PricePerGram ?? 0m)
                : product.PurchasePrice;

            var laborPercentageCost = materialCost * product.LaborCostPercentage / 100m;
            return materialCost + product.LaborCost + laborPercentageCost + product.AdditionalCost;
        }

        decimal CalculateCurrentSalePrice(Domain.Entities.Product product, decimal purchaseCost)
            => product.SalePrice > 0 ? product.SalePrice : purchaseCost * (1 + product.ProfitMarginPercentage / 100m);

        return new ProfitLossSummaryDto
        {
            TotalRevenue = sales.Sum(x => x.TotalPrice),
            TotalCostOfSoldGoods = sales.Sum(x => x.TotalCostSnapshot),
            TotalMaterialCostOfSales = sales.Sum(x => x.MaterialCostSnapshot * x.Quantity),
            TotalLaborCostOfSales = sales.Sum(x => x.LaborCostSnapshot * x.Quantity),
            TotalAdditionalCostOfSales = sales.Sum(x => x.AdditionalCostSnapshot * x.Quantity),
            TotalInvestmentAmount = buys.Sum(x => x.TotalPrice),
            InventoryEstimatedCost = products.Sum(x => x.StockQuantity * CalculateCurrentPurchaseCost(x)),
            InventoryEstimatedRevenue = products.Sum(x => x.StockQuantity * CalculateCurrentSalePrice(x, CalculateCurrentPurchaseCost(x))),
            NetProfitOrLoss = sales.Sum(x => x.ProfitOrLoss),
            TotalSalesTransactions = sales.Count,
            TotalBuyTransactions = buys.Count,
            ProductBreakdown = sales
                .GroupBy(x => new { x.ProductId, ProductName = x.Product!.Name })
                .Select(group => new ProfitLossByProductDto
                {
                    ProductId = group.Key.ProductId,
                    ProductName = group.Key.ProductName,
                    ProfitOrLoss = group.Sum(x => x.ProfitOrLoss),
                    Revenue = group.Sum(x => x.TotalPrice),
                    Cost = group.Sum(x => x.TotalCostSnapshot)
                })
                .OrderByDescending(x => x.ProfitOrLoss)
                .ToList()
        };
    }
}
