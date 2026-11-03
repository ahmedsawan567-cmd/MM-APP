using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MMApplication.Infrastructure.Data;

public static class DatabaseSchemaAligner
{
    public static async Task EnsureSchemaAlignedAsync(AppDbContext context, ILogger logger)
    {
        if (!context.Database.IsRelational())
        {
            await context.Database.EnsureCreatedAsync();
            return;
        }

        var providerName = context.Database.ProviderName ?? "";

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
            providerName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            await EnsurePostgreSqlSchemaAsync(context, logger);
        }
        else if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureSqlServerSchemaAsync(context, logger);
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }
    }

    private static async Task EnsureSqlServerSchemaAsync(AppDbContext context, ILogger logger)
    {
        logger.LogInformation("Verifying and aligning SQL Server database schema with extended enterprise specification...");

        var sql = @"
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- 1. المتاجر والمستأجرين داخل المول
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Tenants')
BEGIN
    CREATE TABLE Tenants (
        TenantId NVARCHAR(50) PRIMARY KEY,
        NameAr NVARCHAR(150) NOT NULL,
        NameEn NVARCHAR(150) NOT NULL,
        UnitNumber NVARCHAR(20) NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        LogoUrl NVARCHAR(500) NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

-- 2. قواعد الولاء لكل متجر
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantLoyaltyRules')
BEGIN
    CREATE TABLE TenantLoyaltyRules (
        RuleId INT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL UNIQUE FOREIGN KEY REFERENCES Tenants(TenantId) ON DELETE CASCADE,
        RewardType NVARCHAR(20) NOT NULL,
        ConversionRate DECIMAL(5, 4) NOT NULL,
        MinInvoiceTotal DECIMAL(18, 2) DEFAULT 0.00,
        MaxPointsCap INT NULL,
        PointsExpiryDays INT DEFAULT 365,
        MaxInvoiceAgeDays INT DEFAULT 14,
        DynamicRulesJson NVARCHAR(MAX) NULL,
        UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

-- 3. موظفو ومستخدمو النظام
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemUsers')
BEGIN
    CREATE TABLE SystemUsers (
        UserId NVARCHAR(50) PRIMARY KEY,
        TenantId NVARCHAR(50) NULL FOREIGN KEY REFERENCES Tenants(TenantId) ON DELETE SET NULL,
        FullName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(150) NOT NULL UNIQUE,
        PhoneNumber NVARCHAR(20) NULL,
        PasswordHash NVARCHAR(256) NOT NULL,
        Role NVARCHAR(30) NOT NULL,
        PinCodeHash NVARCHAR(256) NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

-- 4. المتسوقين والعملاء
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE Customers (
        CustomerId NVARCHAR(50) PRIMARY KEY,
        FullName NVARCHAR(100) NOT NULL,
        PhoneNumber NVARCHAR(20) NOT NULL UNIQUE,
        DeviceFingerprint NVARCHAR(100) NULL,
        LoyaltyTier NVARCHAR(20) DEFAULT 'BRONZE',
        IsFrozen BIT DEFAULT 0,
        IsArchived BIT DEFAULT 0,
        RegisteredAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'IsArchived')
    BEGIN
        ALTER TABLE Customers ADD IsArchived BIT DEFAULT 0;
    END
END

-- 5. الفواتير وعمليات المسح بالذكاء الاصطناعي
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Invoices')
BEGIN
    CREATE TABLE Invoices (
        InvoiceId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CustomerId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Customers(CustomerId),
        TenantId NVARCHAR(50) NULL FOREIGN KEY REFERENCES Tenants(TenantId),
        ExtractedMerchant NVARCHAR(150) NULL,
        InvoiceNumber NVARCHAR(100) NULL,
        InvoiceDate DATETIME2 NULL,
        TotalAmount DECIMAL(18, 2) NULL,
        ConfidenceScore DECIMAL(5, 4) NULL,
        RawAiPayload NVARCHAR(MAX) NULL,
        ReceiptImageUrl NVARCHAR(500) NOT NULL,
        ImagePHash NVARCHAR(64) NULL,
        FraudRiskScore INT DEFAULT 0,
        ProcessingStatus NVARCHAR(30) NOT NULL DEFAULT 'PENDING',
        RejectionReason NVARCHAR(250) NULL,
        ResubmissionNotes NVARCHAR(500) NULL,
        ResubmissionRequestedAt DATETIME2 NULL,
        DisputeReason NVARCHAR(500) NULL,
        DisputedAt DATETIME2 NULL,
        PointsAwarded INT DEFAULT 0,
        BasePointsEarned INT DEFAULT 0,
        ItemBonusPointsEarned INT DEFAULT 0,
        PointsBreakdownJson NVARCHAR(MAX) NULL,
        CashbackAwarded DECIMAL(18, 2) DEFAULT 0.00,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Tenant_InvoiceNumber' AND object_id = OBJECT_ID('Invoices'))
    BEGIN
        CREATE UNIQUE INDEX UQ_Tenant_InvoiceNumber 
        ON Invoices (TenantId, InvoiceNumber) 
        WHERE TenantId IS NOT NULL AND InvoiceNumber IS NOT NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_Customer_Date' AND object_id = OBJECT_ID('Invoices'))
    BEGIN
        CREATE INDEX IX_Invoices_Customer_Date ON Invoices (CustomerId, CreatedAt);
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_PHash' AND object_id = OBJECT_ID('Invoices'))
    BEGIN
        CREATE INDEX IX_Invoices_PHash ON Invoices (ImagePHash);
    END
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'ResubmissionNotes')
    BEGIN
        ALTER TABLE Invoices ADD ResubmissionNotes NVARCHAR(500) NULL;
        ALTER TABLE Invoices ADD ResubmissionRequestedAt DATETIME2 NULL;
        ALTER TABLE Invoices ADD DisputeReason NVARCHAR(500) NULL;
        ALTER TABLE Invoices ADD DisputedAt DATETIME2 NULL;
    END
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'BasePointsEarned')
    BEGIN
        ALTER TABLE Invoices ADD BasePointsEarned INT DEFAULT 0;
        ALTER TABLE Invoices ADD ItemBonusPointsEarned INT DEFAULT 0;
        ALTER TABLE Invoices ADD PointsBreakdownJson NVARCHAR(MAX) NULL;
    END
END

-- 5.1 قواعد احتساب النقاط المرنة (Flexible Loyalty Earning Rules)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoyaltyEarningRules')
BEGIN
    CREATE TABLE LoyaltyEarningRules (
        RuleId INT IDENTITY(1,1) PRIMARY KEY,
        StoreTenantId NVARCHAR(50) NULL FOREIGN KEY REFERENCES Tenants(TenantId) ON DELETE SET NULL,
        RuleName NVARCHAR(200) NOT NULL,
        CalculationType NVARCHAR(50) NOT NULL DEFAULT 'SPEND_RATIO',
        FixedPointsPerInvoice INT DEFAULT 0,
        SpendRatioRate DECIMAL(5, 4) DEFAULT 0.1000,
        MinInvoiceAmount DECIMAL(18, 2) DEFAULT 0.00,
        MaxPointsCap INT NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL
    );
    CREATE INDEX IX_LoyaltyEarningRules_Tenant ON LoyaltyEarningRules (StoreTenantId);
    CREATE INDEX IX_LoyaltyEarningRules_Type ON LoyaltyEarningRules (CalculationType);
END

-- 6. الأصناف المسحوبة من الفاتورة
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InvoiceLineItems')
BEGIN
    CREATE TABLE InvoiceLineItems (
        LineItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
        InvoiceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES Invoices(InvoiceId) ON DELETE CASCADE,
        ItemName NVARCHAR(200) NOT NULL,
        Quantity INT DEFAULT 1,
        UnitPrice DECIMAL(18, 2) NOT NULL,
        TotalPrice DECIMAL(18, 2) NOT NULL,
        Confidence DECIMAL(5, 4) NULL
    );
END

-- 7. المحافظ الرقمية للمتسوقين
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerWallets')
BEGIN
    CREATE TABLE CustomerWallets (
        WalletId BIGINT IDENTITY(1,1) PRIMARY KEY,
        CustomerId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Customers(CustomerId),
        TenantId NVARCHAR(50) NULL FOREIGN KEY REFERENCES Tenants(TenantId),
        BalanceType NVARCHAR(20) NOT NULL,
        CurrentBalance DECIMAL(18, 2) DEFAULT 0.00,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_Customer_Tenant_Wallet UNIQUE (CustomerId, TenantId, BalanceType)
    );
END

-- 8. دفتر أستاذ المحفظة وتتبع الحركات
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WalletLedger')
BEGIN
    CREATE TABLE WalletLedger (
        EntryId BIGINT IDENTITY(1,1) PRIMARY KEY,
        WalletId BIGINT NOT NULL FOREIGN KEY REFERENCES CustomerWallets(WalletId),
        InvoiceId UNIQUEIDENTIFIER NULL FOREIGN KEY REFERENCES Invoices(InvoiceId),
        TransactionType NVARCHAR(30) NOT NULL,
        AmountChanged DECIMAL(18, 2) NOT NULL,
        BalanceAfter DECIMAL(18, 2) NOT NULL,
        ExpiresAt DATETIME2 NULL,
        ReferenceRemarks NVARCHAR(255) NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

-- 9. المقاصة والتسويات المالية للمتاجر
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantSettlements')
BEGIN
    CREATE TABLE TenantSettlements (
        SettlementId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Tenants(TenantId),
        PeriodStartDate DATETIME2 NOT NULL,
        PeriodEndDate DATETIME2 NOT NULL,
        TotalVouchersRedeemedValue DECIMAL(18, 2) NOT NULL,
        MallServiceFee DECIMAL(18, 2) NOT NULL,
        NetPayableToTenant DECIMAL(18, 2) NOT NULL,
        SettlementStatus NVARCHAR(30) NOT NULL DEFAULT 'OPEN',
        SettledByUserId NVARCHAR(50) NULL FOREIGN KEY REFERENCES SystemUsers(UserId),
        SettledAt DATETIME2 NULL
    );
END

-- 10. سجلات تدقيق الاحتيال
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FraudReviewLogs')
BEGIN
    CREATE TABLE FraudReviewLogs (
        ReviewId BIGINT IDENTITY(1,1) PRIMARY KEY,
        InvoiceId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES Invoices(InvoiceId),
        AssignedAuditorId NVARCHAR(50) NULL FOREIGN KEY REFERENCES SystemUsers(UserId),
        CalculatedRiskScore INT NOT NULL,
        TriggeredFlags NVARCHAR(MAX) NULL,
        AuditorDecision NVARCHAR(30) NULL,
        DecisionReason NVARCHAR(255) NULL,
        ResolvedAt DATETIME2 NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

-- 11. ترويسات وأسماء المتاجر البديلة
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StoreAliases')
BEGIN
    CREATE TABLE StoreAliases (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Tenants(TenantId) ON DELETE CASCADE,
        AliasName NVARCHAR(200) NOT NULL,
        MatchType NVARCHAR(30) DEFAULT 'CONTAINS',
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_StoreAliases_AliasName ON StoreAliases (AliasName);
    CREATE INDEX IX_StoreAliases_TenantId ON StoreAliases (TenantId);
END

-- 12. سجل بصمات الأجهزة لمنع الحسابات المتعددة
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerDevices')
BEGIN
    CREATE TABLE CustomerDevices (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        CustomerId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Customers(CustomerId) ON DELETE CASCADE,
        DeviceFingerprint NVARCHAR(150) NOT NULL,
        DeviceModel NVARCHAR(100) NULL,
        OsVersion NVARCHAR(50) NULL,
        IpAddress NVARCHAR(50) NULL,
        FirstSeenAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        LastSeenAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_CustomerDevices_Fingerprint ON CustomerDevices (DeviceFingerprint);
    CREATE INDEX IX_CustomerDevices_CustomerId ON CustomerDevices (CustomerId);
END

-- 13. قواعد محفزات المنتجات والماركات وسلة المشتريات
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductBoosterRules')
BEGIN
    CREATE TABLE ProductBoosterRules (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Title NVARCHAR(200) NOT NULL,
        BrandOrItemKeyword NVARCHAR(200) NOT NULL,
        TargetCategory NVARCHAR(50) NULL,
        TenantId NVARCHAR(50) NULL FOREIGN KEY REFERENCES Tenants(TenantId) ON DELETE SET NULL,
        BonusPoints INT DEFAULT 20,
        PointsMultiplier DECIMAL(5, 2) DEFAULT 1.00,
        SponsorName NVARCHAR(200) NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ProductBooster_Keyword ON ProductBoosterRules (BrandOrItemKeyword);
    CREATE INDEX IX_ProductBooster_TenantId ON ProductBoosterRules (TenantId);
END

-- Auxiliary Tables
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Rewards')
BEGIN
    CREATE TABLE Rewards (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Title NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        MerchantName NVARCHAR(150) NULL,
        PointsCost INT NOT NULL,
        DiscountValue DECIMAL(18, 2) NOT NULL,
        ImageUrl NVARCHAR(500) NULL,
        StockQuantity INT NULL,
        IsActive BIT DEFAULT 1,
        ValidityDays INT DEFAULT 30,
        ValidUntil DATETIME2 NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RewardRedemptions')
BEGIN
    CREATE TABLE RewardRedemptions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CustomerId NVARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Customers(CustomerId),
        RewardId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES Rewards(Id),
        RedemptionCode NVARCHAR(50) NOT NULL UNIQUE,
        PointsSpent INT NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
        RedeemedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
        UsedAt DATETIME2 NULL,
        UsedByMerchant NVARCHAR(200) NULL,
        ExpiresAt DATETIME2 NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PromotionBanners')
BEGIN
    CREATE TABLE PromotionBanners (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Title NVARCHAR(200) NOT NULL,
        Subtitle NVARCHAR(300) NULL,
        ImageUrl NVARCHAR(500) NOT NULL,
        TargetUrl NVARCHAR(500) NULL,
        ActionType NVARCHAR(50) DEFAULT 'EXTERNAL_URL',
        ActionPayloadJson NVARCHAR(MAX) NULL,
        TargetAudience NVARCHAR(50) DEFAULT 'ALL',
        DisplayOrder INT DEFAULT 0,
        IsActive BIT DEFAULT 1,
        StartDate DATETIME2 NULL,
        EndDate DATETIME2 NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PromotionBanners') AND name = 'ActionType')
    BEGIN
        ALTER TABLE PromotionBanners ADD ActionType NVARCHAR(50) DEFAULT 'EXTERNAL_URL';
        ALTER TABLE PromotionBanners ADD ActionPayloadJson NVARCHAR(MAX) NULL;
    END
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LoyaltyCampaigns')
BEGIN
    CREATE TABLE LoyaltyCampaigns (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        PointsMultiplier DECIMAL(4, 2) NOT NULL,
        MinSpendAmount DECIMAL(18, 2) NOT NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PerformedBy NVARCHAR(150) NOT NULL,
        Action NVARCHAR(100) NOT NULL,
        EntityName NVARCHAR(100) NOT NULL,
        EntityId NVARCHAR(100) NULL,
        Details NVARCHAR(1000) NOT NULL,
        IpAddress NVARCHAR(50) NULL,
        CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
    );
END
";

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql);
            logger.LogInformation("SQL Server database schema successfully verified and aligned.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SQL Server Schema alignment notice: {Message}", ex.Message);
        }
    }

    private static async Task EnsurePostgreSqlSchemaAsync(AppDbContext context, ILogger logger)
    {
        logger.LogInformation("Verifying and aligning PostgreSQL / Supabase database schema with extended enterprise specification...");

        var sql = @"
-- 1. المتاجر والمستأجرين داخل المول
CREATE TABLE IF NOT EXISTS ""Tenants"" (
    ""TenantId"" VARCHAR(50) PRIMARY KEY,
    ""NameAr"" VARCHAR(150) NOT NULL,
    ""NameEn"" VARCHAR(150) NOT NULL,
    ""UnitNumber"" VARCHAR(20) NOT NULL,
    ""Category"" VARCHAR(50) NOT NULL,
    ""LogoUrl"" VARCHAR(500) NULL,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2. قواعد الولاء لكل متجر
CREATE TABLE IF NOT EXISTS ""TenantLoyaltyRules"" (
    ""RuleId"" SERIAL PRIMARY KEY,
    ""TenantId"" VARCHAR(50) NOT NULL UNIQUE REFERENCES ""Tenants""(""TenantId"") ON DELETE CASCADE,
    ""RewardType"" VARCHAR(20) NOT NULL,
    ""ConversionRate"" NUMERIC(5, 4) NOT NULL,
    ""MinInvoiceTotal"" NUMERIC(18, 2) DEFAULT 0.00,
    ""MaxPointsCap"" INT NULL,
    ""PointsExpiryDays"" INT DEFAULT 365,
    ""MaxInvoiceAgeDays"" INT DEFAULT 14,
    ""DynamicRulesJson"" TEXT NULL,
    ""UpdatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 3. موظفو ومستخدمو النظام
CREATE TABLE IF NOT EXISTS ""SystemUsers"" (
    ""UserId"" VARCHAR(50) PRIMARY KEY,
    ""TenantId"" VARCHAR(50) NULL REFERENCES ""Tenants""(""TenantId"") ON DELETE SET NULL,
    ""FullName"" VARCHAR(100) NOT NULL,
    ""Email"" VARCHAR(150) NOT NULL UNIQUE,
    ""PhoneNumber"" VARCHAR(20) NULL,
    ""PasswordHash"" VARCHAR(256) NOT NULL,
    ""Role"" VARCHAR(30) NOT NULL,
    ""PinCodeHash"" VARCHAR(256) NULL,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 4. المتسوقين والعملاء
CREATE TABLE IF NOT EXISTS ""Customers"" (
    ""CustomerId"" VARCHAR(50) PRIMARY KEY,
    ""FullName"" VARCHAR(100) NOT NULL,
    ""PhoneNumber"" VARCHAR(20) NOT NULL UNIQUE,
    ""DeviceFingerprint"" VARCHAR(100) NULL,
    ""LoyaltyTier"" VARCHAR(20) DEFAULT 'BRONZE',
    ""IsFrozen"" BOOLEAN DEFAULT FALSE,
    ""IsArchived"" BOOLEAN DEFAULT FALSE,
    ""RegisteredAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""IsArchived"" BOOLEAN DEFAULT FALSE;

-- 5. الفواتير وعمليات المسح بالذكاء الاصطناعي
CREATE TABLE IF NOT EXISTS ""Invoices"" (
    ""InvoiceId"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""CustomerId"" VARCHAR(50) NOT NULL REFERENCES ""Customers""(""CustomerId""),
    ""TenantId"" VARCHAR(50) NULL REFERENCES ""Tenants""(""TenantId""),
    ""ExtractedMerchant"" VARCHAR(150) NULL,
    ""InvoiceNumber"" VARCHAR(100) NULL,
    ""InvoiceDate"" TIMESTAMPTZ NULL,
    ""TotalAmount"" NUMERIC(18, 2) NULL,
    ""ConfidenceScore"" NUMERIC(5, 4) NULL,
    ""RawAiPayload"" TEXT NULL,
    ""ReceiptImageUrl"" VARCHAR(500) NOT NULL,
    ""ImagePHash"" VARCHAR(64) NULL,
    ""FraudRiskScore"" INT DEFAULT 0,
    ""ProcessingStatus"" VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    ""RejectionReason"" VARCHAR(250) NULL,
    ""ResubmissionNotes"" TEXT NULL,
    ""ResubmissionRequestedAt"" TIMESTAMPTZ NULL,
    ""DisputeReason"" TEXT NULL,
    ""DisputedAt"" TIMESTAMPTZ NULL,
    ""PointsAwarded"" INT DEFAULT 0,
    ""BasePointsEarned"" INT DEFAULT 0,
    ""ItemBonusPointsEarned"" INT DEFAULT 0,
    ""PointsBreakdownJson"" TEXT NULL,
    ""CashbackAwarded"" NUMERIC(18, 2) DEFAULT 0.00,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ResubmissionNotes"" TEXT NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ResubmissionRequestedAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""DisputeReason"" TEXT NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""DisputedAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""BasePointsEarned"" INT DEFAULT 0;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ItemBonusPointsEarned"" INT DEFAULT 0;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""PointsBreakdownJson"" TEXT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ""UQ_Tenant_InvoiceNumber"" 
ON ""Invoices"" (""TenantId"", ""InvoiceNumber"") 
WHERE ""TenantId"" IS NOT NULL AND ""InvoiceNumber"" IS NOT NULL;

CREATE INDEX IF NOT EXISTS ""IX_Invoices_Customer_Date"" ON ""Invoices"" (""CustomerId"", ""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_Invoices_PHash"" ON ""Invoices"" (""ImagePHash"");

-- 5.1 قواعد احتساب النقاط المرنة (Flexible Loyalty Earning Rules)
CREATE TABLE IF NOT EXISTS ""LoyaltyEarningRules"" (
    ""RuleId"" SERIAL PRIMARY KEY,
    ""StoreTenantId"" VARCHAR(50) NULL REFERENCES ""Tenants""(""TenantId"") ON DELETE SET NULL,
    ""RuleName"" VARCHAR(200) NOT NULL,
    ""CalculationType"" VARCHAR(50) NOT NULL DEFAULT 'SPEND_RATIO',
    ""FixedPointsPerInvoice"" INT DEFAULT 0,
    ""SpendRatioRate"" NUMERIC(5, 4) DEFAULT 0.1000,
    ""MinInvoiceAmount"" NUMERIC(18, 2) DEFAULT 0.00,
    ""MaxPointsCap"" INT NULL,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp(),
    ""UpdatedAt"" TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ""IX_LoyaltyEarningRules_Tenant"" ON ""LoyaltyEarningRules"" (""StoreTenantId"");
CREATE INDEX IF NOT EXISTS ""IX_LoyaltyEarningRules_Type"" ON ""LoyaltyEarningRules"" (""CalculationType"");

-- 6. الأصناف المسحوبة من الفاتورة
CREATE TABLE IF NOT EXISTS ""InvoiceLineItems"" (
    ""LineItemId"" BIGSERIAL PRIMARY KEY,
    ""InvoiceId"" UUID NOT NULL REFERENCES ""Invoices""(""InvoiceId"") ON DELETE CASCADE,
    ""ItemName"" VARCHAR(200) NOT NULL,
    ""Quantity"" INT DEFAULT 1,
    ""UnitPrice"" NUMERIC(18, 2) NOT NULL,
    ""TotalPrice"" NUMERIC(18, 2) NOT NULL,
    ""Confidence"" NUMERIC(5, 4) NULL
);

-- 7. المحافظ الرقمية للمتسوقين
CREATE TABLE IF NOT EXISTS ""CustomerWallets"" (
    ""WalletId"" BIGSERIAL PRIMARY KEY,
    ""CustomerId"" VARCHAR(50) NOT NULL REFERENCES ""Customers""(""CustomerId""),
    ""TenantId"" VARCHAR(50) NULL REFERENCES ""Tenants""(""TenantId""),
    ""BalanceType"" VARCHAR(20) NOT NULL,
    ""CurrentBalance"" NUMERIC(18, 2) DEFAULT 0.00,
    ""RowVersion"" BYTEA NULL,
    CONSTRAINT ""UQ_Customer_Tenant_Wallet"" UNIQUE (""CustomerId"", ""TenantId"", ""BalanceType"")
);

-- 8. دفتر أستاذ المحفظة وتتبع الحركات
CREATE TABLE IF NOT EXISTS ""WalletLedger"" (
    ""EntryId"" BIGSERIAL PRIMARY KEY,
    ""WalletId"" BIGINT NOT NULL REFERENCES ""CustomerWallets""(""WalletId""),
    ""InvoiceId"" UUID NULL REFERENCES ""Invoices""(""InvoiceId""),
    ""TransactionType"" VARCHAR(30) NOT NULL,
    ""AmountChanged"" NUMERIC(18, 2) NOT NULL,
    ""BalanceAfter"" NUMERIC(18, 2) NOT NULL,
    ""ExpiresAt"" TIMESTAMPTZ NULL,
    ""ReferenceRemarks"" VARCHAR(255) NULL,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 9. المقاصة والتسويات المالية للمتاجر
CREATE TABLE IF NOT EXISTS ""TenantSettlements"" (
    ""SettlementId"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""TenantId"" VARCHAR(50) NOT NULL REFERENCES ""Tenants""(""TenantId""),
    ""PeriodStartDate"" DATE NOT NULL,
    ""PeriodEndDate"" DATE NOT NULL,
    ""TotalVouchersRedeemedValue"" NUMERIC(18, 2) NOT NULL,
    ""MallServiceFee"" NUMERIC(18, 2) NOT NULL,
    ""NetPayableToTenant"" NUMERIC(18, 2) NOT NULL,
    ""SettlementStatus"" VARCHAR(30) NOT NULL DEFAULT 'OPEN',
    ""SettledByUserId"" VARCHAR(50) NULL REFERENCES ""SystemUsers""(""UserId""),
    ""SettledAt"" TIMESTAMPTZ NULL
);

-- 10. سجلات تدقيق الاحتيال
CREATE TABLE IF NOT EXISTS ""FraudReviewLogs"" (
    ""ReviewId"" BIGSERIAL PRIMARY KEY,
    ""InvoiceId"" UUID NOT NULL REFERENCES ""Invoices""(""InvoiceId""),
    ""AssignedAuditorId"" VARCHAR(50) NULL REFERENCES ""SystemUsers""(""UserId""),
    ""CalculatedRiskScore"" INT NOT NULL,
    ""TriggeredFlags"" TEXT NULL,
    ""AuditorDecision"" VARCHAR(30) NULL,
    ""DecisionReason"" VARCHAR(255) NULL,
    ""ResolvedAt"" TIMESTAMPTZ NULL,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 11. ترويسات وأسماء المتاجر البديلة
CREATE TABLE IF NOT EXISTS ""StoreAliases"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""TenantId"" VARCHAR(50) NOT NULL REFERENCES ""Tenants""(""TenantId"") ON DELETE CASCADE,
    ""AliasName"" VARCHAR(200) NOT NULL,
    ""MatchType"" VARCHAR(30) DEFAULT 'CONTAINS',
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);
CREATE INDEX IF NOT EXISTS ""IX_StoreAliases_AliasName"" ON ""StoreAliases"" (""AliasName"");
CREATE INDEX IF NOT EXISTS ""IX_StoreAliases_TenantId"" ON ""StoreAliases"" (""TenantId"");

-- 12. سجل بصمات الأجهزة لمنع الحسابات المتعددة
CREATE TABLE IF NOT EXISTS ""CustomerDevices"" (
    ""Id"" BIGSERIAL PRIMARY KEY,
    ""CustomerId"" VARCHAR(50) NOT NULL REFERENCES ""Customers""(""CustomerId"") ON DELETE CASCADE,
    ""DeviceFingerprint"" VARCHAR(150) NOT NULL,
    ""DeviceModel"" VARCHAR(100) NULL,
    ""OsVersion"" VARCHAR(50) NULL,
    ""IpAddress"" VARCHAR(50) NULL,
    ""FirstSeenAt"" TIMESTAMPTZ DEFAULT clock_timestamp(),
    ""LastSeenAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);
CREATE INDEX IF NOT EXISTS ""IX_CustomerDevices_Fingerprint"" ON ""CustomerDevices"" (""DeviceFingerprint"");
CREATE INDEX IF NOT EXISTS ""IX_CustomerDevices_CustomerId"" ON ""CustomerDevices"" (""CustomerId"");

-- 13. قواعد محفزات المنتجات والماركات وسلة المشتريات
CREATE TABLE IF NOT EXISTS ""ProductBoosterRules"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""Title"" VARCHAR(200) NOT NULL,
    ""BrandOrItemKeyword"" VARCHAR(200) NOT NULL,
    ""TargetCategory"" VARCHAR(50) NULL,
    ""TenantId"" VARCHAR(50) NULL REFERENCES ""Tenants""(""TenantId"") ON DELETE SET NULL,
    ""BonusPoints"" INT DEFAULT 20,
    ""PointsMultiplier"" NUMERIC(5, 2) DEFAULT 1.00,
    ""SponsorName"" VARCHAR(200) NULL,
    ""StartDate"" TIMESTAMPTZ NOT NULL,
    ""EndDate"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);
CREATE INDEX IF NOT EXISTS ""IX_ProductBooster_Keyword"" ON ""ProductBoosterRules"" (""BrandOrItemKeyword"");
CREATE INDEX IF NOT EXISTS ""IX_ProductBooster_TenantId"" ON ""ProductBoosterRules"" (""TenantId"");

-- Auxiliary Tables
CREATE TABLE IF NOT EXISTS ""Rewards"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""Title"" VARCHAR(150) NOT NULL,
    ""Description"" VARCHAR(500) NOT NULL,
    ""MerchantName"" VARCHAR(150) NULL,
    ""PointsCost"" INT NOT NULL,
    ""DiscountValue"" NUMERIC(18, 2) NOT NULL,
    ""ImageUrl"" VARCHAR(500) NULL,
    ""StockQuantity"" INT NULL,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""ValidityDays"" INT DEFAULT 30,
    ""ValidUntil"" TIMESTAMPTZ NULL,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS ""RewardRedemptions"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""CustomerId"" VARCHAR(50) NOT NULL REFERENCES ""Customers""(""CustomerId""),
    ""RewardId"" UUID NOT NULL REFERENCES ""Rewards""(""Id""),
    ""RedemptionCode"" VARCHAR(50) NOT NULL UNIQUE,
    ""PointsSpent"" INT NOT NULL,
    ""Status"" VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
    ""RedeemedAt"" TIMESTAMPTZ DEFAULT clock_timestamp(),
    ""UsedAt"" TIMESTAMPTZ NULL,
    ""UsedByMerchant"" VARCHAR(200) NULL,
    ""ExpiresAt"" TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS ""PromotionBanners"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""Title"" VARCHAR(200) NOT NULL,
    ""Subtitle"" VARCHAR(300) NULL,
    ""ImageUrl"" VARCHAR(500) NOT NULL,
    ""TargetUrl"" VARCHAR(500) NULL,
    ""ActionType"" VARCHAR(50) DEFAULT 'EXTERNAL_URL',
    ""ActionPayloadJson"" TEXT NULL,
    ""TargetAudience"" VARCHAR(50) DEFAULT 'ALL',
    ""DisplayOrder"" INT DEFAULT 0,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""StartDate"" TIMESTAMPTZ NULL,
    ""EndDate"" TIMESTAMPTZ NULL,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

ALTER TABLE ""PromotionBanners"" ADD COLUMN IF NOT EXISTS ""ActionType"" VARCHAR(50) DEFAULT 'EXTERNAL_URL';
ALTER TABLE ""PromotionBanners"" ADD COLUMN IF NOT EXISTS ""ActionPayloadJson"" TEXT NULL;

CREATE TABLE IF NOT EXISTS ""LoyaltyCampaigns"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""Name"" VARCHAR(200) NOT NULL,
    ""Description"" VARCHAR(500) NOT NULL,
    ""PointsMultiplier"" NUMERIC(4, 2) NOT NULL,
    ""MinSpendAmount"" NUMERIC(18, 2) NOT NULL,
    ""StartDate"" TIMESTAMPTZ NOT NULL,
    ""EndDate"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

CREATE TABLE IF NOT EXISTS ""AuditLogs"" (
    ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ""PerformedBy"" VARCHAR(150) NOT NULL,
    ""Action"" VARCHAR(100) NOT NULL,
    ""EntityName"" VARCHAR(100) NOT NULL,
    ""EntityId"" VARCHAR(100) NULL,
    ""Details"" VARCHAR(1000) NOT NULL,
    ""IpAddress"" VARCHAR(50) NULL,
    ""CreatedAt"" TIMESTAMPTZ DEFAULT clock_timestamp()
);

";

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql);
            logger.LogInformation("PostgreSQL / Supabase database schema successfully verified and aligned.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PostgreSQL Schema alignment notice: {Message}", ex.Message);
        }
    }
}
