using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'PasswordHash')
    BEGIN
        ALTER TABLE Customers ADD PasswordHash NVARCHAR(256) NULL;
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

    /// <summary>
    /// مزامنة مخطط PostgreSQL / Supabase مع نموذج البيانات في التطبيق (بدون حذف أي بيانات):
    /// - قاعدة فارغة: ينشئ EF كل الجداول بالضبط كما يحتاجها التطبيق.
    /// - قاعدة موجودة (مثلاً بعد تشغيل supabase/schema.sql): ينشئ الجداول الناقصة ويضيف الأعمدة الناقصة فقط.
    /// </summary>
    private static async Task EnsurePostgreSqlSchemaAsync(AppDbContext context, ILogger logger)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync())
        {
            throw new InvalidOperationException("قاعدة البيانات غير موجودة أو لا يمكن الوصول إليها. تحقق من رابط الاتصال.");
        }

        var existing = await ReadExistingColumnsAsync(context);
        if (existing.Count == 0)
        {
            logger.LogInformation("PostgreSQL: قاعدة فارغة — إنشاء كل الجداول من نموذج البيانات.");
            await creator.CreateTablesAsync();
            return;
        }

        // 1) الجداول الناقصة: من سكربت الإنشاء الذي يولّده EF
        var statements = context.Database.GenerateCreateScript()
            .Split(";" + Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim().TrimEnd(';'))
            .Where(x => x.Length > 0)
            .ToList();

        var tables = context.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => t != null)
            .Distinct()
            .ToList();
        var missingTables = tables.Where(t => !existing.ContainsKey(t!)).Select(t => t!).ToList();

        foreach (var table in missingTables)
        {
            var create = statements.FirstOrDefault(x => x.StartsWith($"CREATE TABLE \"{table}\"", StringComparison.Ordinal));
            if (create == null) continue;
            logger.LogInformation("PostgreSQL: إنشاء الجدول الناقص {Table}", table);
            await ExecuteAsync(context, create);
        }
        foreach (var index in statements.Where(x => x.StartsWith("CREATE ", StringComparison.Ordinal) && x.Contains(" INDEX ", StringComparison.Ordinal)))
        {
            if (missingTables.Any(t => index.Contains($" ON \"{t}\"", StringComparison.Ordinal)))
            {
                await ExecuteAsync(context, index);
            }
        }

        // 2) الأعمدة الناقصة في الجداول الموجودة
        if (missingTables.Count > 0) existing = await ReadExistingColumnsAsync(context);
        var added = 0;
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null || !existing.TryGetValue(table, out var columns)) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column == null || columns.Contains(column)) continue;

                var type = property.GetColumnType(store);
                var nullable = property.IsColumnNullable(store);
                var fallback = DefaultFor(type);
                var sql = $"ALTER TABLE \"{table}\" ADD COLUMN IF NOT EXISTS \"{column}\" {type}" +
                          (!nullable && fallback != null ? $" NOT NULL DEFAULT {fallback}" : "");
                logger.LogInformation("PostgreSQL: إضافة العمود الناقص {Table}.{Column}", table, column);
                await ExecuteAsync(context, sql);
                columns.Add(column);
                added++;
            }
        }

        logger.LogInformation("PostgreSQL: المخطط متوافق ({Tables} جدول جديد، {Columns} عمود جديد).", missingTables.Count, added);
    }

    private static string? DefaultFor(string type)
    {
        var t = type.ToLowerInvariant();
        if (t.StartsWith("int") || t.StartsWith("bigint") || t.StartsWith("smallint") || t.StartsWith("numeric") ||
            t.StartsWith("double") || t.StartsWith("real") || t.StartsWith("decimal")) return "0";
        if (t.StartsWith("boolean")) return "FALSE";
        if (t.StartsWith("text") || t.StartsWith("character")) return "''";
        if (t.StartsWith("uuid")) return "gen_random_uuid()";
        if (t.StartsWith("timestamp")) return "now()";
        return null;
    }

    private static async Task<Dictionary<string, HashSet<string>>> ReadExistingColumnsAsync(AppDbContext context)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = current_schema()";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                if (!result.TryGetValue(table, out var set)) result[table] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(reader.GetString(1));
            }
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
        return result;
    }

    // ExecuteSqlRaw يعامل الأقواس المعقوفة كمعاملات؛ أوامر المخطط لا تحتوي معاملات فنمررها كما هي
    private static Task ExecuteAsync(AppDbContext context, string sql) =>
        context.Database.ExecuteSqlRawAsync(sql.Replace("{", "{{").Replace("}", "}}"));
}
