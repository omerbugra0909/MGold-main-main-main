/*
  MGold production-safe upgrade preflight.
  This block adopts an existing MGold SQL Server schema when tables already exist
  but __EFMigrationsHistory is missing or incomplete. It prevents "object already exists"
  errors on live databases that were installed manually before EF migration history existed.
*/

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
AND OBJECT_ID(N'[AppUsers]', N'U') IS NOT NULL
AND OBJECT_ID(N'[Products]', N'U') IS NOT NULL
AND OBJECT_ID(N'[Orders]', N'U') IS NOT NULL
AND OBJECT_ID(N'[MarketQuoteSnapshots]', N'U') IS NOT NULL
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605205013_InitialProductionSchema', N'8.0.15');
END;

IF OBJECT_ID(N'[MarketQuoteSnapshots]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'MarketQuoteSnapshots', N'CalculationBasis') IS NULL
        ALTER TABLE [MarketQuoteSnapshots] ADD [CalculationBasis] nvarchar(500) NULL;

    IF COL_LENGTH(N'MarketQuoteSnapshots', N'DataQualityStatus') IS NULL
        ALTER TABLE [MarketQuoteSnapshots] ADD [DataQualityStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_MarketQuoteSnapshots_DataQualityStatus] DEFAULT N'ok';

    IF COL_LENGTH(N'MarketQuoteSnapshots', N'QualityWarningsJson') IS NULL
        ALTER TABLE [MarketQuoteSnapshots] ADD [QualityWarningsJson] nvarchar(2000) NOT NULL CONSTRAINT [DF_MarketQuoteSnapshots_QualityWarningsJson] DEFAULT N'[]';

    IF COL_LENGTH(N'MarketQuoteSnapshots', N'SourceType') IS NULL
        ALTER TABLE [MarketQuoteSnapshots] ADD [SourceType] nvarchar(40) NOT NULL CONSTRAINT [DF_MarketQuoteSnapshots_SourceType] DEFAULT N'live_market';

    IF NOT EXISTS (
        SELECT 1 FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
    )
    AND COL_LENGTH(N'MarketQuoteSnapshots', N'CalculationBasis') IS NOT NULL
    AND COL_LENGTH(N'MarketQuoteSnapshots', N'DataQualityStatus') IS NOT NULL
    AND COL_LENGTH(N'MarketQuoteSnapshots', N'QualityWarningsJson') IS NOT NULL
    AND COL_LENGTH(N'MarketQuoteSnapshots', N'SourceType') IS NOT NULL
    BEGIN
        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260607141759_MarketQuoteQualityMetadata', N'8.0.15');
    END;
END;

IF OBJECT_ID(N'[Companies]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Companies', N'Categories') IS NULL
        ALTER TABLE [Companies] ADD [Categories] nvarchar(300) NULL;

    IF COL_LENGTH(N'Companies', N'City') IS NULL
        ALTER TABLE [Companies] ADD [City] nvarchar(80) NULL;

    IF COL_LENGTH(N'Companies', N'CoverImageUrl') IS NULL
        ALTER TABLE [Companies] ADD [CoverImageUrl] nvarchar(260) NULL;

    IF COL_LENGTH(N'Companies', N'Description') IS NULL
        ALTER TABLE [Companies] ADD [Description] nvarchar(600) NULL;

    IF COL_LENGTH(N'Companies', N'District') IS NULL
        ALTER TABLE [Companies] ADD [District] nvarchar(80) NULL;

    IF COL_LENGTH(N'Companies', N'LogoUrl') IS NULL
        ALTER TABLE [Companies] ADD [LogoUrl] nvarchar(260) NULL;

    IF COL_LENGTH(N'Companies', N'SearchKeywords') IS NULL
        ALTER TABLE [Companies] ADD [SearchKeywords] nvarchar(300) NULL;

    IF COL_LENGTH(N'Companies', N'SocialLinks') IS NULL
        ALTER TABLE [Companies] ADD [SocialLinks] nvarchar(400) NULL;

    IF COL_LENGTH(N'Companies', N'TaxNumber') IS NULL
        ALTER TABLE [Companies] ADD [TaxNumber] nvarchar(40) NULL;

    IF COL_LENGTH(N'Companies', N'TaxOffice') IS NULL
        ALTER TABLE [Companies] ADD [TaxOffice] nvarchar(120) NULL;

    IF COL_LENGTH(N'Companies', N'WebsiteUrl') IS NULL
        ALTER TABLE [Companies] ADD [WebsiteUrl] nvarchar(180) NULL;

    IF COL_LENGTH(N'Companies', N'WorkingHours') IS NULL
        ALTER TABLE [Companies] ADD [WorkingHours] nvarchar(600) NULL;

    IF NOT EXISTS (
        SELECT 1 FROM [__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
    )
    AND COL_LENGTH(N'Companies', N'Categories') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'City') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'CoverImageUrl') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'Description') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'District') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'LogoUrl') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'SearchKeywords') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'SocialLinks') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'TaxNumber') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'TaxOffice') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'WebsiteUrl') IS NOT NULL
    AND COL_LENGTH(N'Companies', N'WorkingHours') IS NOT NULL
    BEGIN
        INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260607212600_CompanyTenantProfile', N'8.0.15');
    END;
END;

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [Username] nvarchar(80) NULL,
        [UserRole] nvarchar(30) NULL,
        [ActionType] nvarchar(20) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(80) NULL,
        [HttpMethod] nvarchar(10) NOT NULL,
        [Path] nvarchar(300) NOT NULL,
        [CorrelationId] nvarchar(100) NULL,
        [IsSuccess] bit NOT NULL,
        [StatusCode] int NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        [BeforeState] nvarchar(max) NULL,
        [AfterState] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(140) NOT NULL,
        [Code] nvarchar(80) NULL,
        [Address] nvarchar(160) NULL,
        [ContactEmail] nvarchar(150) NULL,
        [ContactPhone] nvarchar(30) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [ContactMessages] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [Subject] nvarchar(180) NOT NULL,
        [Message] nvarchar(2500) NOT NULL,
        [IsResolved] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolutionNote] nvarchar(500) NULL,
        CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [GoldPrices] (
        [Id] int NOT NULL IDENTITY,
        [PricePerGram] decimal(18,2) NOT NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [Source] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GoldPrices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [MarketProviderConfigurations] (
        [Id] int NOT NULL IDENTITY,
        [ProviderKey] nvarchar(80) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [SupportsRealtime] bit NOT NULL,
        [Priority] int NOT NULL,
        [RefreshIntervalSeconds] int NOT NULL,
        [BaseUrl] nvarchar(200) NULL,
        [ApiKey] nvarchar(200) NULL,
        [LastSuccessfulSyncAt] datetime2 NULL,
        [LastFailureAt] datetime2 NULL,
        [LastError] nvarchar(400) NULL,
        [FailureCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MarketProviderConfigurations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [MarketQuoteSnapshots] (
        [Id] int NOT NULL IDENTITY,
        [Symbol] nvarchar(40) NOT NULL,
        [DisplayName] nvarchar(160) NOT NULL,
        [Category] int NOT NULL,
        [UnitLabel] nvarchar(32) NOT NULL,
        [NativeCurrency] nvarchar(12) NOT NULL,
        [PriceInUsd] decimal(18,6) NOT NULL,
        [Price24hAgoInUsd] decimal(18,6) NOT NULL,
        [High24hInUsd] decimal(18,6) NOT NULL,
        [Low24hInUsd] decimal(18,6) NOT NULL,
        [SparklineJson] nvarchar(4000) NOT NULL,
        [ProviderKey] nvarchar(80) NOT NULL,
        [ProviderDisplayName] nvarchar(120) NOT NULL,
        [Note] nvarchar(400) NULL,
        [IsFallback] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [LastUpdatedAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MarketQuoteSnapshots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(160) NOT NULL,
        [Message] nvarchar(1200) NOT NULL,
        [Type] int NOT NULL,
        [TargetRole] nvarchar(30) NULL,
        [RelatedEntityName] nvarchar(80) NULL,
        [RelatedEntityId] nvarchar(80) NULL,
        [IsRead] bit NOT NULL,
        [IsCritical] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NULL,
        [Name] nvarchar(120) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [Email] nvarchar(150) NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Customers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NULL,
        [Name] nvarchar(120) NOT NULL,
        [Type] int NOT NULL,
        [Weight] decimal(18,3) NOT NULL,
        [PurityRate] decimal(10,4) NOT NULL,
        [LaborCost] decimal(18,2) NOT NULL,
        [LaborCostPercentage] decimal(18,2) NOT NULL,
        [AdditionalCost] decimal(18,2) NOT NULL,
        [ProfitMarginPercentage] decimal(18,2) NOT NULL,
        [PurchasePrice] decimal(18,2) NOT NULL,
        [SalePrice] decimal(18,2) NOT NULL,
        [StockQuantity] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [AppUsers] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(80) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Role] nvarchar(30) NOT NULL,
        [CompanyId] int NULL,
        [CustomerId] int NULL,
        [CreatedByUserId] int NULL,
        [IsActive] bit NOT NULL,
        [EmailConfirmed] bit NOT NULL,
        [EmailConfirmedAt] datetime2 NULL,
        [PhoneConfirmed] bit NOT NULL,
        [PhoneConfirmedAt] datetime2 NULL,
        [AccessFailedCount] int NOT NULL,
        [LockoutEndAt] datetime2 NULL,
        [SecurityStamp] nvarchar(80) NOT NULL,
        [ThemePreference] nvarchar(40) NOT NULL,
        [PasswordChangedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AppUsers_AppUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AppUsers] ([Id]),
        CONSTRAINT [FK_AppUsers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
        CONSTRAINT [FK_AppUsers_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [CustomerFavorites] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [ProductId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerFavorites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerFavorites_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
        CONSTRAINT [FK_CustomerFavorites_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [ProductReviews] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [CustomerId] int NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NOT NULL,
        [Status] int NOT NULL,
        [AdminReply] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ModeratedAt] datetime2 NULL,
        CONSTRAINT [PK_ProductReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductReviews_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
        CONSTRAINT [FK_ProductReviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [Transactions] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NULL,
        [ProductId] int NOT NULL,
        [CustomerId] int NULL,
        [Type] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TotalPrice] decimal(18,2) NOT NULL,
        [GoldPricePerGramSnapshot] decimal(18,2) NOT NULL,
        [ProductWeightSnapshot] decimal(18,3) NOT NULL,
        [PurityRateSnapshot] decimal(10,4) NOT NULL,
        [MaterialCostSnapshot] decimal(18,2) NOT NULL,
        [LaborCostSnapshot] decimal(18,2) NOT NULL,
        [LaborCostPercentageSnapshot] decimal(18,2) NOT NULL,
        [AdditionalCostSnapshot] decimal(18,2) NOT NULL,
        [ProfitMarginPercentageSnapshot] decimal(18,2) NOT NULL,
        [CalculatedPurchasePriceSnapshot] decimal(18,2) NOT NULL,
        [CalculatedSalePriceSnapshot] decimal(18,2) NOT NULL,
        [TotalCostSnapshot] decimal(18,2) NOT NULL,
        [ProfitOrLoss] decimal(18,2) NOT NULL,
        [Date] datetime2 NOT NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transactions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
        CONSTRAINT [FK_Transactions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
        CONSTRAINT [FK_Transactions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [AccountVerificationTokens] (
        [Id] int NOT NULL IDENTITY,
        [AppUserId] int NOT NULL,
        [Purpose] nvarchar(40) NOT NULL,
        [Channel] nvarchar(16) NOT NULL,
        [Destination] nvarchar(180) NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [Attempts] int NOT NULL,
        [SendCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastSentAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [RequestIp] nvarchar(80) NULL,
        CONSTRAINT [PK_AccountVerificationTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountVerificationTokens_AppUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AppUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [MarketWatchlistItems] (
        [Id] int NOT NULL IDENTITY,
        [AppUserId] int NOT NULL,
        [Symbol] nvarchar(40) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MarketWatchlistItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MarketWatchlistItems_AppUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AppUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NULL,
        [OrderNumber] nvarchar(40) NOT NULL,
        [CustomerId] int NOT NULL,
        [Status] int NOT NULL,
        [Notes] nvarchar(500) NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [PaymentStatus] int NOT NULL,
        [PreferredPaymentMethod] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [DueDate] datetime2 NULL,
        [AssignedEmployeeUserId] int NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_AppUsers_AssignedEmployeeUserId] FOREIGN KEY ([AssignedEmployeeUserId]) REFERENCES [AppUsers] ([Id]),
        CONSTRAINT [FK_Orders_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
        CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] int NOT NULL IDENTITY,
        [AppUserId] int NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [SecurityStampSnapshot] nvarchar(128) NOT NULL,
        [DeviceName] nvarchar(160) NULL,
        [CreatedByIp] nvarchar(80) NULL,
        [CreatedByUserAgent] nvarchar(300) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedByIp] nvarchar(80) NULL,
        [ReplacedByTokenHash] nvarchar(128) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AppUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AppUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [WorkTasks] (
        [Id] int NOT NULL IDENTITY,
        [CompanyId] int NOT NULL,
        [Title] nvarchar(140) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [DueDate] datetime2 NULL,
        [AssignedToUserId] int NOT NULL,
        [AssignedByUserId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_WorkTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkTasks_AppUsers_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [AppUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_AppUsers_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [AppUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [OrderHistoryEntries] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Type] int NOT NULL,
        [Title] nvarchar(140) NOT NULL,
        [Description] nvarchar(1200) NOT NULL,
        [ActorUsername] nvarchar(80) NULL,
        [ActorRole] nvarchar(30) NULL,
        [RelatedEntityName] nvarchar(80) NULL,
        [RelatedEntityId] nvarchar(80) NULL,
        [MetadataJson] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderHistoryEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderHistoryEntries_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [OrderInvoices] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [InvoiceNumber] nvarchar(40) NOT NULL,
        [FilePath] nvarchar(260) NOT NULL,
        [FileName] nvarchar(160) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [InvoiceDate] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderInvoices_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TotalPrice] decimal(18,2) NOT NULL,
        [Notes] nvarchar(300) NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]),
        CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [OrderPayments] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Method] int NOT NULL,
        [Status] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReferenceNumber] nvarchar(120) NULL,
        [ProviderKey] nvarchar(80) NULL,
        [ProviderTransactionId] nvarchar(160) NULL,
        [IdempotencyKey] nvarchar(120) NULL,
        [InstallmentCount] int NOT NULL,
        [RequiresThreeDSecure] bit NOT NULL,
        [ThreeDSecureStatus] nvarchar(40) NULL,
        [ParentPaymentId] int NULL,
        [IsRefund] bit NOT NULL,
        [IsPartialRefund] bit NOT NULL,
        [FailureCode] nvarchar(80) NULL,
        [FailureMessage] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,
        [PaidAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByUsername] nvarchar(80) NULL,
        CONSTRAINT [PK_OrderPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderPayments_OrderPayments_ParentPaymentId] FOREIGN KEY ([ParentPaymentId]) REFERENCES [OrderPayments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderPayments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE TABLE [WorkTaskHistoryEntries] (
        [Id] int NOT NULL IDENTITY,
        [WorkTaskId] int NOT NULL,
        [ActionTitle] nvarchar(140) NOT NULL,
        [Description] nvarchar(1200) NULL,
        [PreviousStatus] int NULL,
        [NewStatus] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WorkTaskHistoryEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkTaskHistoryEntries_AppUsers_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [AppUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTaskHistoryEntries_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AccountVerificationTokens_AppUserId_Purpose_Channel_ConsumedAt] ON [AccountVerificationTokens] ([AppUserId], [Purpose], [Channel], [ConsumedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AccountVerificationTokens_ExpiresAt] ON [AccountVerificationTokens] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_CompanyId] ON [AppUsers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_CreatedByUserId] ON [AppUsers] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_CustomerId] ON [AppUsers] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_Email] ON [AppUsers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_Phone] ON [AppUsers] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_Username] ON [AppUsers] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CorrelationId] ON [AuditLogs] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName] ON [AuditLogs] ([EntityName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Companies_Code] ON [Companies] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Companies_Name] ON [Companies] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_ContactMessages_CreatedAt] ON [ContactMessages] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_CustomerFavorites_CreatedAt] ON [CustomerFavorites] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerFavorites_CustomerId_ProductId] ON [CustomerFavorites] ([CustomerId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_CustomerFavorites_ProductId] ON [CustomerFavorites] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Customers_CompanyId] ON [Customers] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketProviderConfigurations_IsEnabled_Priority] ON [MarketProviderConfigurations] ([IsEnabled], [Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MarketProviderConfigurations_ProviderKey] ON [MarketProviderConfigurations] ([ProviderKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketQuoteSnapshots_Category] ON [MarketQuoteSnapshots] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketQuoteSnapshots_LastUpdatedAt] ON [MarketQuoteSnapshots] ([LastUpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MarketQuoteSnapshots_Symbol] ON [MarketQuoteSnapshots] ([Symbol]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MarketWatchlistItems_AppUserId_Symbol] ON [MarketWatchlistItems] ([AppUserId], [Symbol]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketWatchlistItems_CreatedAt] ON [MarketWatchlistItems] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAt] ON [Notifications] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Notifications_TargetRole_IsRead] ON [Notifications] ([TargetRole], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderHistoryEntries_CreatedAt] ON [OrderHistoryEntries] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderHistoryEntries_OrderId] ON [OrderHistoryEntries] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderInvoices_InvoiceDate] ON [OrderInvoices] ([InvoiceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderInvoices_InvoiceNumber] ON [OrderInvoices] ([InvoiceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderInvoices_OrderId] ON [OrderInvoices] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderPayments_CreatedAt] ON [OrderPayments] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrderPayments_IdempotencyKey] ON [OrderPayments] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderPayments_OrderId] ON [OrderPayments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderPayments_ParentPaymentId] ON [OrderPayments] ([ParentPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrderPayments_ProviderKey_ProviderTransactionId] ON [OrderPayments] ([ProviderKey], [ProviderTransactionId]) WHERE [ProviderKey] IS NOT NULL AND [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_AssignedEmployeeUserId] ON [Orders] ([AssignedEmployeeUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_CompanyId] ON [Orders] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_CustomerId] ON [Orders] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_ProductReviews_CustomerId] ON [ProductReviews] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_ProductReviews_ProductId] ON [ProductReviews] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Products_CompanyId] ON [Products] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_AppUserId_ExpiresAt_RevokedAt] ON [RefreshTokens] ([AppUserId], [ExpiresAt], [RevokedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_CompanyId] ON [Transactions] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_CustomerId] ON [Transactions] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_ProductId] ON [Transactions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTaskHistoryEntries_ActorUserId] ON [WorkTaskHistoryEntries] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTaskHistoryEntries_WorkTaskId_CreatedAt] ON [WorkTaskHistoryEntries] ([WorkTaskId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_AssignedByUserId] ON [WorkTasks] ([AssignedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_AssignedToUserId] ON [WorkTasks] ([AssignedToUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_CompanyId_Status_DueDate] ON [WorkTasks] ([CompanyId], [Status], [DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605205013_InitialProductionSchema', N'8.0.15');
END;

COMMIT;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [CalculationBasis] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [DataQualityStatus] nvarchar(40) NOT NULL DEFAULT N'ok';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [QualityWarningsJson] nvarchar(2000) NOT NULL DEFAULT N'[]';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [SourceType] nvarchar(40) NOT NULL DEFAULT N'live_market';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260607141759_MarketQuoteQualityMetadata', N'8.0.15');
END;

COMMIT;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [Categories] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [City] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [CoverImageUrl] nvarchar(260) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [Description] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [District] nvarchar(80) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [LogoUrl] nvarchar(260) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [SearchKeywords] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [SocialLinks] nvarchar(400) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [TaxNumber] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [TaxOffice] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [WebsiteUrl] nvarchar(180) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [WorkingHours] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260607212600_CompanyTenantProfile', N'8.0.15');
END;

COMMIT;

/*
  MGold production bootstrap data.
  Ensures the platform admin and company manager accounts exist after schema install/repair.
*/
IF OBJECT_ID(N'[Companies]', N'U') IS NOT NULL AND OBJECT_ID(N'[AppUsers]', N'U') IS NOT NULL
BEGIN
    DECLARE @now datetime2 = SYSUTCDATETIME();
    DECLARE @passwordHash nvarchar(256) = N'AQAAAAIAAYagAAAAECQ7s/BRLOjitHzX3ogNpQOPiA+NVmbANkeqMwcCKwfsZX0BnJuL/MeGTNbWKznzSQ==';
    DECLARE @companyId int;

    IF EXISTS (SELECT 1 FROM [Companies] WHERE [Code] = N'MGOLD' OR [Name] = N'MGold Kuyumculuk')
    BEGIN
        UPDATE [Companies]
        SET [Name] = N'MGold Kuyumculuk',
            [Code] = N'MGOLD',
            [ContactEmail] = N'sakizciomerbugra@gmail.com',
            [ContactPhone] = N'+905510840483',
            [Address] = N'Hürriyet, Onikişubat / Kahramanmaraş',
            [IsActive] = 1
        WHERE [Code] = N'MGOLD' OR [Name] = N'MGold Kuyumculuk';
    END
    ELSE
    BEGIN
        INSERT INTO [Companies] ([Name], [Code], [Address], [ContactEmail], [ContactPhone], [IsActive], [CreatedAt])
        VALUES (N'MGold Kuyumculuk', N'MGOLD', N'Hürriyet, Onikişubat / Kahramanmaraş', N'sakizciomerbugra@gmail.com', N'+905510840483', 1, @now);
    END

    SELECT TOP 1 @companyId = [Id] FROM [Companies] WHERE [Code] = N'MGOLD' ORDER BY [Id];

    DECLARE @adminId int;
    SELECT TOP 1 @adminId = [Id]
    FROM [AppUsers]
    WHERE [Email] = N'sakizciomerbugra@gmail.com'
       OR [Username] = N'platform.admin'
       OR [Phone] = N'+905510840483'
    ORDER BY CASE WHEN [Role] = N'SystemAdmin' THEN 0 ELSE 1 END, [Id];

    UPDATE [AppUsers]
    SET [IsActive] = 0,
        [LockoutEndAt] = DATEADD(year, 100, @now),
        [Username] = LEFT(CONCAT(N'disabled.', [Id], N'.', [Username]), 80),
        [Email] = LEFT(CONCAT(N'disabled.', [Id], N'.', [Email]), 150),
        [Phone] = CONCAT(N'+909', RIGHT(CONCAT(N'000000000', CONVERT(nvarchar(20), [Id])), 9)),
        [SecurityStamp] = REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')
    WHERE (@adminId IS NULL OR [Id] <> @adminId)
      AND ([Email] = N'sakizciomerbugra@gmail.com' OR [Username] = N'platform.admin' OR [Phone] = N'+905510840483');

    IF @adminId IS NULL
    BEGIN
        INSERT INTO [AppUsers] ([Username], [FullName], [Email], [Phone], [PasswordHash], [Role], [CompanyId], [CustomerId], [CreatedByUserId], [IsActive], [EmailConfirmed], [EmailConfirmedAt], [PhoneConfirmed], [PhoneConfirmedAt], [AccessFailedCount], [LockoutEndAt], [SecurityStamp], [ThemePreference], [PasswordChangedAt], [CreatedAt], [LastLoginAt])
        VALUES (N'platform.admin', N'Sistem Yöneticisi', N'sakizciomerbugra@gmail.com', N'+905510840483', @passwordHash, N'SystemAdmin', NULL, NULL, NULL, 1, 1, @now, 1, @now, 0, NULL, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), N'gold-premium', @now, @now, NULL);
        SET @adminId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE [AppUsers]
        SET [Username] = N'platform.admin',
            [FullName] = N'Sistem Yöneticisi',
            [Email] = N'sakizciomerbugra@gmail.com',
            [Phone] = N'+905510840483',
            [PasswordHash] = @passwordHash,
            [Role] = N'SystemAdmin',
            [CompanyId] = NULL,
            [CustomerId] = NULL,
            [CreatedByUserId] = NULL,
            [IsActive] = 1,
            [EmailConfirmed] = 1,
            [EmailConfirmedAt] = COALESCE([EmailConfirmedAt], @now),
            [PhoneConfirmed] = 1,
            [PhoneConfirmedAt] = COALESCE([PhoneConfirmedAt], @now),
            [AccessFailedCount] = 0,
            [LockoutEndAt] = NULL,
            [SecurityStamp] = COALESCE(NULLIF([SecurityStamp], N''), REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')),
            [ThemePreference] = COALESCE(NULLIF([ThemePreference], N''), N'gold-premium'),
            [PasswordChangedAt] = @now
        WHERE [Id] = @adminId;
    END

    DECLARE @managerId int;
    SELECT TOP 1 @managerId = [Id]
    FROM [AppUsers]
    WHERE [Email] IN (N'sakizciomerbugra895@gmail.com', N'sakizciomerbugra895gmail.com')
       OR [Username] IN (N'firma.yoneticisi', N'firma.yöneticisi')
       OR [Phone] = N'+905510840484'
    ORDER BY CASE WHEN [Role] = N'Manager' THEN 0 ELSE 1 END, [Id];

    UPDATE [AppUsers]
    SET [IsActive] = 0,
        [LockoutEndAt] = DATEADD(year, 100, @now),
        [Username] = LEFT(CONCAT(N'disabled.', [Id], N'.', [Username]), 80),
        [Email] = LEFT(CONCAT(N'disabled.', [Id], N'.', [Email]), 150),
        [Phone] = CONCAT(N'+909', RIGHT(CONCAT(N'000000000', CONVERT(nvarchar(20), [Id])), 9)),
        [SecurityStamp] = REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')
    WHERE (@managerId IS NULL OR [Id] <> @managerId)
      AND ([Email] IN (N'sakizciomerbugra895@gmail.com', N'sakizciomerbugra895gmail.com') OR [Username] IN (N'firma.yoneticisi', N'firma.yöneticisi') OR [Phone] = N'+905510840484');

    IF @managerId IS NULL
    BEGIN
        INSERT INTO [AppUsers] ([Username], [FullName], [Email], [Phone], [PasswordHash], [Role], [CompanyId], [CustomerId], [CreatedByUserId], [IsActive], [EmailConfirmed], [EmailConfirmedAt], [PhoneConfirmed], [PhoneConfirmedAt], [AccessFailedCount], [LockoutEndAt], [SecurityStamp], [ThemePreference], [PasswordChangedAt], [CreatedAt], [LastLoginAt])
        VALUES (N'firma.yoneticisi', N'Firma Yöneticisi', N'sakizciomerbugra895@gmail.com', N'+905510840484', @passwordHash, N'Manager', @companyId, NULL, @adminId, 1, 1, @now, 1, @now, 0, NULL, REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N''), N'gold-premium', @now, @now, NULL);
    END
    ELSE
    BEGIN
        UPDATE [AppUsers]
        SET [Username] = N'firma.yoneticisi',
            [FullName] = N'Firma Yöneticisi',
            [Email] = N'sakizciomerbugra895@gmail.com',
            [Phone] = N'+905510840484',
            [PasswordHash] = @passwordHash,
            [Role] = N'Manager',
            [CompanyId] = @companyId,
            [CustomerId] = NULL,
            [CreatedByUserId] = @adminId,
            [IsActive] = 1,
            [EmailConfirmed] = 1,
            [EmailConfirmedAt] = COALESCE([EmailConfirmedAt], @now),
            [PhoneConfirmed] = 1,
            [PhoneConfirmedAt] = COALESCE([PhoneConfirmedAt], @now),
            [AccessFailedCount] = 0,
            [LockoutEndAt] = NULL,
            [SecurityStamp] = COALESCE(NULLIF([SecurityStamp], N''), REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'')),
            [ThemePreference] = COALESCE(NULLIF([ThemePreference], N''), N'gold-premium'),
            [PasswordChangedAt] = @now
        WHERE [Id] = @managerId;
    END
END;
/*
  MGold safe market fallback seed.
  Keeps market pages non-empty until live providers refresh the snapshots.
*/
IF OBJECT_ID(N'[MarketQuoteSnapshots]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [MarketQuoteSnapshots])
BEGIN
    DECLARE @marketNow datetime2 = SYSUTCDATETIME();
    DECLARE @quality nvarchar(2000) = N'["Güvenli başlangıç verisi; canlı kaynak güncellenince değişir."]';

    INSERT INTO [MarketQuoteSnapshots]
        ([Symbol], [DisplayName], [Category], [UnitLabel], [NativeCurrency], [PriceInUsd], [Price24hAgoInUsd], [High24hInUsd], [Low24hInUsd], [SparklineJson], [ProviderKey], [ProviderDisplayName], [Note], [IsFallback], [SortOrder], [LastUpdatedAt], [CreatedAt], [CalculationBasis], [DataQualityStatus], [QualityWarningsJson], [SourceType])
    VALUES
        (N'TRY', N'Türk Lirası', 2, N'1 USD', N'TRY', 0.031000, 0.031100, 0.031200, 0.030800, N'[0.0311,0.0308,0.0310,0.0312]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 1, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'USD', N'Amerikan Doları', 2, N'1 USD', N'USD', 1.000000, 1.000000, 1.000000, 1.000000, N'[1,1,1,1]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 2, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'EUR', N'Euro', 2, N'1 EUR', N'USD', 1.080000, 1.079000, 1.083000, 1.074000, N'[1.079,1.074,1.08,1.083]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 3, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'GBP', N'İngiliz Sterlini', 2, N'1 GBP', N'USD', 1.270000, 1.268000, 1.276000, 1.263000, N'[1.268,1.263,1.27,1.276]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 4, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'XAU', N'Ons Altın', 1, N'ons', N'USD', 2350.000000, 2342.000000, 2362.000000, 2331.000000, N'[2342,2331,2350,2362]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 10, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'GRAM_ALTIN', N'Gram Altın', 1, N'gr', N'USD', 75.550000, 75.200000, 76.100000, 74.800000, N'[75.2,74.8,75.55,76.1]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 11, @marketNow, @marketNow, N'Ons Altın / 31.1034768', N'stale', @quality, N'derived_formula'),
        (N'CEYREK_ALTIN', N'Çeyrek Altın', 1, N'adet', N'USD', 132.500000, 131.900000, 133.400000, 130.800000, N'[131.9,130.8,132.5,133.4]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 12, @marketNow, @marketNow, N'Gram Altın * 1.754', N'stale', @quality, N'derived_formula'),
        (N'YARIM_ALTIN', N'Yarım Altın', 1, N'adet', N'USD', 265.000000, 263.800000, 266.800000, 261.600000, N'[263.8,261.6,265,266.8]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 13, @marketNow, @marketNow, N'Çeyrek Altın * 2', N'stale', @quality, N'derived_formula'),
        (N'TAM_ALTIN', N'Tam Altın', 1, N'adet', N'USD', 530.000000, 527.600000, 533.600000, 523.200000, N'[527.6,523.2,530,533.6]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 14, @marketNow, @marketNow, N'Yarım Altın * 2', N'stale', @quality, N'derived_formula'),
        (N'XAG', N'Gümüş', 3, N'ons', N'USD', 30.500000, 30.200000, 30.900000, 29.800000, N'[30.2,29.8,30.5,30.9]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 20, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'XPT', N'Platin', 3, N'ons', N'USD', 1010.000000, 1004.000000, 1022.000000, 996.000000, N'[1004,996,1010,1022]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 21, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'BRENT', N'Brent Petrol', 5, N'varil', N'USD', 82.000000, 81.300000, 83.100000, 80.700000, N'[81.3,80.7,82,83.1]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 30, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'BTC', N'Bitcoin', 6, N'adet', N'USD', 68000.000000, 67250.000000, 68900.000000, 66500.000000, N'[67250,66500,68000,68900]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 40, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback'),
        (N'ETH', N'Ethereum', 6, N'adet', N'USD', 3600.000000, 3560.000000, 3650.000000, 3510.000000, N'[3560,3510,3600,3650]', N'fallback-core', N'MGold Güvenli Başlangıç Verisi', N'Dış piyasa servisleri güncellenene kadar kullanılan güvenli başlangıç snapshotı.', 1, 41, @marketNow, @marketNow, NULL, N'stale', @quality, N'manual_fallback');
END;

IF OBJECT_ID(N'[GoldPrices]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [GoldPrices] WHERE [IsActive] = 1)
BEGIN
    INSERT INTO [GoldPrices] ([PricePerGram], [EffectiveFrom], [Source], [IsActive], [CreatedAt])
    VALUES (2437.10, SYSUTCDATETIME(), N'MGold güvenli başlangıç verisi', 1, SYSUTCDATETIME());
END;
