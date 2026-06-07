IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AccountVerificationTokens_AppUserId_Purpose_Channel_ConsumedAt] ON [AccountVerificationTokens] ([AppUserId], [Purpose], [Channel], [ConsumedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AccountVerificationTokens_ExpiresAt] ON [AccountVerificationTokens] ([ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_CompanyId] ON [AppUsers] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_CreatedByUserId] ON [AppUsers] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_CustomerId] ON [AppUsers] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_Email] ON [AppUsers] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_Phone] ON [AppUsers] ([Phone]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_Username] ON [AppUsers] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CorrelationId] ON [AuditLogs] ([CorrelationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName] ON [AuditLogs] ([EntityName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Companies_Code] ON [Companies] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Companies_Name] ON [Companies] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_ContactMessages_CreatedAt] ON [ContactMessages] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_CustomerFavorites_CreatedAt] ON [CustomerFavorites] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerFavorites_CustomerId_ProductId] ON [CustomerFavorites] ([CustomerId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_CustomerFavorites_ProductId] ON [CustomerFavorites] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Customers_CompanyId] ON [Customers] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketProviderConfigurations_IsEnabled_Priority] ON [MarketProviderConfigurations] ([IsEnabled], [Priority]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MarketProviderConfigurations_ProviderKey] ON [MarketProviderConfigurations] ([ProviderKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketQuoteSnapshots_Category] ON [MarketQuoteSnapshots] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketQuoteSnapshots_LastUpdatedAt] ON [MarketQuoteSnapshots] ([LastUpdatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MarketQuoteSnapshots_Symbol] ON [MarketQuoteSnapshots] ([Symbol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MarketWatchlistItems_AppUserId_Symbol] ON [MarketWatchlistItems] ([AppUserId], [Symbol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_MarketWatchlistItems_CreatedAt] ON [MarketWatchlistItems] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAt] ON [Notifications] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Notifications_TargetRole_IsRead] ON [Notifications] ([TargetRole], [IsRead]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderHistoryEntries_CreatedAt] ON [OrderHistoryEntries] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderHistoryEntries_OrderId] ON [OrderHistoryEntries] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderInvoices_InvoiceDate] ON [OrderInvoices] ([InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderInvoices_InvoiceNumber] ON [OrderInvoices] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderInvoices_OrderId] ON [OrderInvoices] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderPayments_CreatedAt] ON [OrderPayments] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrderPayments_IdempotencyKey] ON [OrderPayments] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderPayments_OrderId] ON [OrderPayments] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_OrderPayments_ParentPaymentId] ON [OrderPayments] ([ParentPaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OrderPayments_ProviderKey_ProviderTransactionId] ON [OrderPayments] ([ProviderKey], [ProviderTransactionId]) WHERE [ProviderKey] IS NOT NULL AND [ProviderTransactionId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_AssignedEmployeeUserId] ON [Orders] ([AssignedEmployeeUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_CompanyId] ON [Orders] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Orders_CustomerId] ON [Orders] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_ProductReviews_CustomerId] ON [ProductReviews] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_ProductReviews_ProductId] ON [ProductReviews] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Products_CompanyId] ON [Products] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_AppUserId_ExpiresAt_RevokedAt] ON [RefreshTokens] ([AppUserId], [ExpiresAt], [RevokedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_CompanyId] ON [Transactions] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_CustomerId] ON [Transactions] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_ProductId] ON [Transactions] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTaskHistoryEntries_ActorUserId] ON [WorkTaskHistoryEntries] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTaskHistoryEntries_WorkTaskId_CreatedAt] ON [WorkTaskHistoryEntries] ([WorkTaskId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_AssignedByUserId] ON [WorkTasks] ([AssignedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_AssignedToUserId] ON [WorkTasks] ([AssignedToUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_CompanyId_Status_DueDate] ON [WorkTasks] ([CompanyId], [Status], [DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260605205013_InitialProductionSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260605205013_InitialProductionSchema', N'8.0.15');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [CalculationBasis] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [DataQualityStatus] nvarchar(40) NOT NULL DEFAULT N'ok';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [QualityWarningsJson] nvarchar(2000) NOT NULL DEFAULT N'[]';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    ALTER TABLE [MarketQuoteSnapshots] ADD [SourceType] nvarchar(40) NOT NULL DEFAULT N'live_market';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607141759_MarketQuoteQualityMetadata'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260607141759_MarketQuoteQualityMetadata', N'8.0.15');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [Categories] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [City] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [CoverImageUrl] nvarchar(260) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [Description] nvarchar(600) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [District] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [LogoUrl] nvarchar(260) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [SearchKeywords] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [SocialLinks] nvarchar(400) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [TaxNumber] nvarchar(40) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [TaxOffice] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [WebsiteUrl] nvarchar(180) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    ALTER TABLE [Companies] ADD [WorkingHours] nvarchar(600) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260607212600_CompanyTenantProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260607212600_CompanyTenantProfile', N'8.0.15');
END;
GO

COMMIT;
GO

