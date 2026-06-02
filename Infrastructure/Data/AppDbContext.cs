using Microsoft.EntityFrameworkCore;
using MGold.Domain.Entities;

namespace MGold.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<GoldPrice> GoldPrices => Set<GoldPrice>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<OrderInvoice> OrderInvoices => Set<OrderInvoice>();
    public DbSet<OrderHistoryEntry> OrderHistoryEntries => Set<OrderHistoryEntry>();
    public DbSet<CustomerFavorite> CustomerFavorites => Set<CustomerFavorite>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<MarketProviderConfiguration> MarketProviderConfigurations => Set<MarketProviderConfiguration>();
    public DbSet<MarketQuoteSnapshot> MarketQuoteSnapshots => Set<MarketQuoteSnapshot>();
    public DbSet<MarketWatchlistItem> MarketWatchlistItems => Set<MarketWatchlistItem>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<WorkTaskHistoryEntry> WorkTaskHistoryEntries => Set<WorkTaskHistoryEntry>();
    public DbSet<AccountVerificationToken> AccountVerificationTokens => Set<AccountVerificationToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Code).IsUnique(false);
            entity.Property(x => x.Name).HasMaxLength(140);
            entity.Property(x => x.Code).HasMaxLength(80);
            entity.Property(x => x.Address).HasMaxLength(160);
            entity.Property(x => x.ContactEmail).HasMaxLength(150);
            entity.Property(x => x.ContactPhone).HasMaxLength(30);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Weight).HasPrecision(18, 3);
            entity.Property(x => x.PurityRate).HasPrecision(10, 4);
            entity.Property(x => x.LaborCost).HasPrecision(18, 2);
            entity.Property(x => x.LaborCostPercentage).HasPrecision(18, 2);
            entity.Property(x => x.AdditionalCost).HasPrecision(18, 2);
            entity.Property(x => x.ProfitMarginPercentage).HasPrecision(18, 2);
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.Email).HasMaxLength(150);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Customers)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.Property(x => x.GoldPricePerGramSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.ProductWeightSnapshot).HasPrecision(18, 3);
            entity.Property(x => x.PurityRateSnapshot).HasPrecision(10, 4);
            entity.Property(x => x.MaterialCostSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.LaborCostSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.LaborCostPercentageSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.AdditionalCostSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.ProfitMarginPercentageSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.CalculatedPurchasePriceSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.CalculatedSalePriceSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.TotalCostSnapshot).HasPrecision(18, 2);
            entity.Property(x => x.ProfitOrLoss).HasPrecision(18, 2);

            entity.HasOne(x => x.Product)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GoldPrice>(entity =>
        {
            entity.Property(x => x.PricePerGram).HasPrecision(18, 2);
            entity.Property(x => x.Source).HasMaxLength(100);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.Phone).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(80);
            entity.Property(x => x.FullName).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.PasswordHash).HasMaxLength(256);
            entity.Property(x => x.Role).HasMaxLength(30);
            entity.Property(x => x.SecurityStamp).HasMaxLength(80);
            entity.Property(x => x.ThemePreference).HasMaxLength(40);

            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany(x => x.ManagedUsers)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AccountVerificationToken>(entity =>
        {
            entity.Property(x => x.Purpose).HasMaxLength(40);
            entity.Property(x => x.Channel).HasMaxLength(16);
            entity.Property(x => x.Destination).HasMaxLength(180);
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.Property(x => x.RequestIp).HasMaxLength(80);
            entity.HasIndex(x => new { x.AppUserId, x.Purpose, x.Channel, x.ConsumedAt });
            entity.HasIndex(x => x.ExpiresAt);

            entity.HasOne(x => x.AppUser)
                .WithMany(x => x.VerificationTokens)
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.Property(x => x.SecurityStampSnapshot).HasMaxLength(128);
            entity.Property(x => x.DeviceName).HasMaxLength(160);
            entity.Property(x => x.CreatedByIp).HasMaxLength(80);
            entity.Property(x => x.CreatedByUserAgent).HasMaxLength(300);
            entity.Property(x => x.RevokedByIp).HasMaxLength(80);
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.AppUserId, x.ExpiresAt, x.RevokedAt });

            entity.HasOne(x => x.AppUser)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.ActionType).HasMaxLength(20);
            entity.Property(x => x.EntityName).HasMaxLength(100);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.HttpMethod).HasMaxLength(10);
            entity.Property(x => x.Path).HasMaxLength(300);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.Username).HasMaxLength(80);
            entity.Property(x => x.UserRole).HasMaxLength(30);
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => x.EntityName);
            entity.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.Property(x => x.OrderNumber).HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.AssignedEmployeeUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedEmployeeUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(300);

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderPayment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(120);
            entity.Property(x => x.ProviderKey).HasMaxLength(80);
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(160);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.ThreeDSecureStatus).HasMaxLength(40);
            entity.Property(x => x.FailureCode).HasMaxLength(80);
            entity.Property(x => x.FailureMessage).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.CreatedByUsername).HasMaxLength(80);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.ProviderKey, x.ProviderTransactionId })
                .IsUnique();
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ParentPayment)
                .WithMany()
                .HasForeignKey(x => x.ParentPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderInvoice>(entity =>
        {
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.Property(x => x.InvoiceNumber).HasMaxLength(40);
            entity.Property(x => x.FilePath).HasMaxLength(260);
            entity.Property(x => x.FileName).HasMaxLength(160);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaidAmount).HasPrecision(18, 2);
            entity.HasIndex(x => x.InvoiceDate);

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Invoices)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderHistoryEntry>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(140);
            entity.Property(x => x.Description).HasMaxLength(1200);
            entity.Property(x => x.ActorUsername).HasMaxLength(80);
            entity.Property(x => x.ActorRole).HasMaxLength(30);
            entity.Property(x => x.RelatedEntityName).HasMaxLength(80);
            entity.Property(x => x.RelatedEntityId).HasMaxLength(80);
            entity.Property(x => x.MetadataJson).HasMaxLength(2000);
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.Order)
                .WithMany(x => x.HistoryEntries)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.Property(x => x.AdminReply).HasMaxLength(500);

            entity.HasOne(x => x.Product)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CustomerFavorite>(entity =>
        {
            entity.HasIndex(x => new { x.CustomerId, x.ProductId }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Favorites)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Product)
                .WithMany(x => x.CustomerFavorites)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Message).HasMaxLength(1200);
            entity.Property(x => x.TargetRole).HasMaxLength(30);
            entity.Property(x => x.RelatedEntityName).HasMaxLength(80);
            entity.Property(x => x.RelatedEntityId).HasMaxLength(80);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.TargetRole, x.IsRead });
        });

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(150);
            entity.Property(x => x.Subject).HasMaxLength(180);
            entity.Property(x => x.Message).HasMaxLength(2500);
            entity.Property(x => x.ResolutionNote).HasMaxLength(500);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<MarketProviderConfiguration>(entity =>
        {
            entity.HasIndex(x => x.ProviderKey).IsUnique();
            entity.Property(x => x.ProviderKey).HasMaxLength(80);
            entity.Property(x => x.DisplayName).HasMaxLength(120);
            entity.Property(x => x.BaseUrl).HasMaxLength(200);
            entity.Property(x => x.ApiKey).HasMaxLength(200);
            entity.Property(x => x.LastError).HasMaxLength(400);
            entity.HasIndex(x => new { x.IsEnabled, x.Priority });
        });

        modelBuilder.Entity<MarketQuoteSnapshot>(entity =>
        {
            entity.HasIndex(x => x.Symbol).IsUnique();
            entity.Property(x => x.Symbol).HasMaxLength(40);
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.Property(x => x.UnitLabel).HasMaxLength(32);
            entity.Property(x => x.NativeCurrency).HasMaxLength(12);
            entity.Property(x => x.ProviderKey).HasMaxLength(80);
            entity.Property(x => x.ProviderDisplayName).HasMaxLength(120);
            entity.Property(x => x.Note).HasMaxLength(400);
            entity.Property(x => x.PriceInUsd).HasPrecision(18, 6);
            entity.Property(x => x.Price24hAgoInUsd).HasPrecision(18, 6);
            entity.Property(x => x.High24hInUsd).HasPrecision(18, 6);
            entity.Property(x => x.Low24hInUsd).HasPrecision(18, 6);
            entity.Property(x => x.SparklineJson).HasMaxLength(4000);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.LastUpdatedAt);
        });

        modelBuilder.Entity<MarketWatchlistItem>(entity =>
        {
            entity.HasIndex(x => new { x.AppUserId, x.Symbol }).IsUnique();
            entity.Property(x => x.Symbol).HasMaxLength(40);
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.AppUser)
                .WithMany()
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkTask>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(140);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasIndex(x => new { x.CompanyId, x.Status, x.DueDate });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AssignedToUser)
                .WithMany(x => x.AssignedTasks)
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedByUser)
                .WithMany(x => x.CreatedTasks)
                .HasForeignKey(x => x.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkTaskHistoryEntry>(entity =>
        {
            entity.Property(x => x.ActionTitle).HasMaxLength(140);
            entity.Property(x => x.Description).HasMaxLength(1200);
            entity.HasIndex(x => new { x.WorkTaskId, x.CreatedAt });

            entity.HasOne(x => x.WorkTask)
                .WithMany(x => x.HistoryEntries)
                .HasForeignKey(x => x.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ActorUser)
                .WithMany(x => x.TaskHistoryEntries)
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
