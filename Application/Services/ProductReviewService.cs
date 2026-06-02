using Microsoft.EntityFrameworkCore;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;
using MGold.Application.Mappings;
using MGold.Domain.Constants;
using MGold.Infrastructure.Data;

namespace MGold.Application.Services;

public class ProductReviewService(
    AppDbContext context,
    IAccessControlService accessControlService,
    ICurrentUserService currentUserService) : IProductReviewService
{
    public async Task<IReadOnlyList<ProductReviewDto>> GetByProductAsync(int productId, bool includePending, CancellationToken cancellationToken = default)
    {
        var canModerate = currentUserService.IsInRole(RoleConstants.SystemAdmin)
            || currentUserService.IsInRole(RoleConstants.Manager);

        var query = context.ProductReviews
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .Where(x => x.ProductId == productId);

        if (!includePending || !canModerate)
        {
            query = query.Where(x => x.Status == Domain.Enums.ReviewStatus.Approved);
        }

        if (currentUserService.IsInRole(RoleConstants.Manager) && !currentUserService.IsInRole(RoleConstants.SystemAdmin))
        {
            query = query.Where(x => x.Product.CompanyId == currentUserService.CompanyId);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ProductReviewDto>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureCanReadOperationalData();

        var items = await context.ProductReviews
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .Where(x => x.Status == Domain.Enums.ReviewStatus.Pending)
            .Where(x => currentUserService.IsInRole(RoleConstants.SystemAdmin) || x.Product.CompanyId == currentUserService.CompanyId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<ProductReviewDto> CreateAsync(CreateProductReviewDto dto, CancellationToken cancellationToken = default)
    {
        ValidateCreateReview(dto);

        var product = await context.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with id {dto.ProductId} was not found.");

        if (dto.CustomerId.HasValue)
        {
            var customerExists = await context.Customers.AnyAsync(x => x.Id == dto.CustomerId.Value, cancellationToken);
            if (!customerExists)
            {
                throw new KeyNotFoundException($"Customer with id {dto.CustomerId.Value} was not found.");
            }

            var customerCompanyId = await context.Customers
                .AsNoTracking()
                .Where(x => x.Id == dto.CustomerId.Value)
                .Select(x => x.CompanyId)
                .FirstOrDefaultAsync(cancellationToken);
            if (customerCompanyId.HasValue && product.CompanyId.HasValue && customerCompanyId != product.CompanyId)
            {
                throw new BusinessRuleException("Customer cannot review products from another company.");
            }
        }

        var entity = new Domain.Entities.ProductReview
        {
            ProductId = dto.ProductId,
            CustomerId = dto.CustomerId,
            Rating = dto.Rating,
            Comment = dto.Comment.Trim(),
            Status = Domain.Enums.ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await context.ProductReviews.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.Notifications.AddAsync(new Domain.Entities.Notification
        {
            Title = "Yeni urun yorumu",
            Message = $"{product.Name} icin onay bekleyen yeni bir yorum var.",
            Type = Domain.Enums.NotificationType.Info,
            TargetRole = RoleConstants.Manager,
            RelatedEntityName = nameof(Domain.Entities.ProductReview),
            RelatedEntityId = entity.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        entity.Product = product;
        if (dto.CustomerId.HasValue)
        {
            entity.Customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.CustomerId.Value, cancellationToken);
        }

        return entity.ToDto();
    }

    public async Task<ProductReviewDto> ModerateAsync(int id, ModerateProductReviewDto dto, CancellationToken cancellationToken = default)
    {
        accessControlService.EnsureManagerOrSystemAdmin();
        if (!Enum.IsDefined(dto.Status))
        {
            throw new BusinessRuleException("Review status is invalid.");
        }

        var entity = await context.ProductReviews
            .Include(x => x.Product)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product review with id {id} was not found.");
        accessControlService.EnsureSameCompany(entity.Product?.CompanyId);

        entity.Status = dto.Status;
        entity.AdminReply = string.IsNullOrWhiteSpace(dto.AdminReply) ? null : dto.AdminReply.Trim();
        entity.ModeratedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    private static void ValidateCreateReview(CreateProductReviewDto dto)
    {
        if (dto.ProductId <= 0)
        {
            throw new BusinessRuleException("Product is required.");
        }

        if (dto.CustomerId is <= 0)
        {
            throw new BusinessRuleException("Customer id is invalid.");
        }

        if (dto.Rating is < 1 or > 5)
        {
            throw new BusinessRuleException("Rating must be between 1 and 5.");
        }

        if (string.IsNullOrWhiteSpace(dto.Comment))
        {
            throw new BusinessRuleException("Review comment is required.");
        }
    }
}
