-- ==============================================================================
-- المرأة مول (Almaraa Mall) - Enterprise Unified Database Schema
-- قاعدة البيانات الموحدة الشاملة لمنظومة الإدارة (ASP.NET Core) وتطبيق الهاتف (Mobile App)
-- ==============================================================================

-- 1. EXTENSIONS
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ==============================================================================
-- 2. CORE MANAGEMENT & MALL ENGINE TABLES (جداول المنظومة والشركات)
-- ==============================================================================

-- 2.1 المتاجر والمستأجرين داخل المول (Tenants / Stores)
CREATE TABLE IF NOT EXISTS public."Tenants" (
    "TenantId" VARCHAR(50) PRIMARY KEY,
    "NameAr" VARCHAR(150) NOT NULL,
    "NameEn" VARCHAR(150) NOT NULL,
    "UnitNumber" VARCHAR(20) NOT NULL,
    "Category" VARCHAR(50) NOT NULL,
    "LogoUrl" VARCHAR(500) NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.2 قواعد الولاء لكل متجر (Tenant Loyalty Rules)
CREATE TABLE IF NOT EXISTS public."TenantLoyaltyRules" (
    "RuleId" SERIAL PRIMARY KEY,
    "TenantId" VARCHAR(50) NOT NULL UNIQUE REFERENCES public."Tenants"("TenantId") ON DELETE CASCADE,
    "RewardType" VARCHAR(20) NOT NULL,
    "ConversionRate" NUMERIC(5, 4) NOT NULL,
    "MinInvoiceTotal" NUMERIC(18, 2) DEFAULT 0.00,
    "MaxPointsCap" INT NULL,
    "PointsExpiryDays" INT DEFAULT 365,
    "MaxInvoiceAgeDays" INT DEFAULT 14,
    "DynamicRulesJson" TEXT NULL,
    "UpdatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.3 موظفو ومستخدمو النظام والصلاحيات (System Users)
CREATE TABLE IF NOT EXISTS public."SystemUsers" (
    "UserId" VARCHAR(50) PRIMARY KEY,
    "TenantId" VARCHAR(50) NULL REFERENCES public."Tenants"("TenantId") ON DELETE SET NULL,
    "FullName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(150) NOT NULL UNIQUE,
    "PhoneNumber" VARCHAR(20) NULL,
    "PasswordHash" VARCHAR(256) NOT NULL,
    "Role" VARCHAR(30) NOT NULL,
    "PinCodeHash" VARCHAR(256) NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.4 المتسوقون والعملاء (Customers / Profiles)
CREATE TABLE IF NOT EXISTS public."Customers" (
    "CustomerId" VARCHAR(50) PRIMARY KEY,
    "FullName" VARCHAR(100) NOT NULL,
    "PhoneNumber" VARCHAR(20) NOT NULL UNIQUE,
    "DeviceFingerprint" VARCHAR(100) NULL,
    "LoyaltyTier" VARCHAR(20) DEFAULT 'VIP',
    "IsFrozen" BOOLEAN DEFAULT FALSE,
    "IsArchived" BOOLEAN DEFAULT FALSE,
    "RegisteredAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.5 الفواتير وعمليات المسح بالذكاء الاصطناعي (Invoices & AI Verification)
CREATE TABLE IF NOT EXISTS public."Invoices" (
    "InvoiceId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CustomerId" VARCHAR(50) NOT NULL REFERENCES public."Customers"("CustomerId"),
    "TenantId" VARCHAR(50) NULL REFERENCES public."Tenants"("TenantId"),
    "ExtractedMerchant" VARCHAR(150) NULL,
    "InvoiceNumber" VARCHAR(100) NULL,
    "InvoiceDate" TIMESTAMPTZ NULL,
    "TotalAmount" NUMERIC(18, 2) NULL,
    "ConfidenceScore" NUMERIC(5, 4) NULL,
    "RawAiPayload" TEXT NULL,
    "ReceiptImageUrl" VARCHAR(500) NOT NULL,
    "ImagePHash" VARCHAR(64) NULL,
    "FraudRiskScore" INT DEFAULT 0,
    "ProcessingStatus" VARCHAR(30) NOT NULL DEFAULT 'PENDING',
    "RejectionReason" VARCHAR(250) NULL,
    "ResubmissionNotes" TEXT NULL,
    "ResubmissionRequestedAt" TIMESTAMPTZ NULL,
    "DisputeReason" TEXT NULL,
    "DisputedAt" TIMESTAMPTZ NULL,
    "PointsAwarded" INT DEFAULT 0,
    "BasePointsEarned" INT DEFAULT 0,
    "ItemBonusPointsEarned" INT DEFAULT 0,
    "PointsBreakdownJson" TEXT NULL,
    "CashbackAwarded" NUMERIC(18, 2) DEFAULT 0.00,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.6 أصناف الفاتورة المسحوبة بالـ AI (Invoice Line Items)
CREATE TABLE IF NOT EXISTS public."InvoiceLineItems" (
    "LineItemId" BIGSERIAL PRIMARY KEY,
    "InvoiceId" UUID NOT NULL REFERENCES public."Invoices"("InvoiceId") ON DELETE CASCADE,
    "ItemName" VARCHAR(200) NOT NULL,
    "Quantity" INT DEFAULT 1,
    "UnitPrice" NUMERIC(18, 2) NOT NULL,
    "TotalPrice" NUMERIC(18, 2) NOT NULL,
    "Confidence" NUMERIC(5, 4) NULL
);

-- 2.7 المحافظ الرقمية للمتسوقين (Customer Wallets)
CREATE TABLE IF NOT EXISTS public."CustomerWallets" (
    "WalletId" BIGSERIAL PRIMARY KEY,
    "CustomerId" VARCHAR(50) NOT NULL REFERENCES public."Customers"("CustomerId"),
    "TenantId" VARCHAR(50) NULL REFERENCES public."Tenants"("TenantId"),
    "BalanceType" VARCHAR(20) NOT NULL,
    "CurrentBalance" NUMERIC(18, 2) DEFAULT 0.00,
    "RowVersion" BYTEA NULL,
    CONSTRAINT "UQ_Customer_Tenant_Wallet" UNIQUE ("CustomerId", "TenantId", "BalanceType")
);

-- 2.8 دفتر أستاذ المحفظة وحركات النقاط (Wallet Ledger)
CREATE TABLE IF NOT EXISTS public."WalletLedger" (
    "EntryId" BIGSERIAL PRIMARY KEY,
    "WalletId" BIGINT NOT NULL REFERENCES public."CustomerWallets"("WalletId"),
    "InvoiceId" UUID NULL REFERENCES public."Invoices"("InvoiceId"),
    "TransactionType" VARCHAR(30) NOT NULL,
    "AmountChanged" NUMERIC(18, 2) NOT NULL,
    "BalanceAfter" NUMERIC(18, 2) NOT NULL,
    "ExpiresAt" TIMESTAMPTZ NULL,
    "ReferenceRemarks" VARCHAR(255) NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.9 المقاصة والتسويات المالية بين المول والمحلات (Tenant Settlements)
CREATE TABLE IF NOT EXISTS public."TenantSettlements" (
    "SettlementId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TenantId" VARCHAR(50) NOT NULL REFERENCES public."Tenants"("TenantId"),
    "PeriodStartDate" DATE NOT NULL,
    "PeriodEndDate" DATE NOT NULL,
    "TotalVouchersRedeemedValue" NUMERIC(18, 2) NOT NULL,
    "MallServiceFee" NUMERIC(18, 2) NOT NULL,
    "NetPayableToTenant" NUMERIC(18, 2) NOT NULL,
    "SettlementStatus" VARCHAR(30) NOT NULL DEFAULT 'OPEN',
    "SettledByUserId" VARCHAR(50) NULL REFERENCES public."SystemUsers"("UserId"),
    "SettledAt" TIMESTAMPTZ NULL
);

-- 2.10 سجلات تدقيق الاحتيال والتحليل الرياضي (Fraud Review Logs)
CREATE TABLE IF NOT EXISTS public."FraudReviewLogs" (
    "ReviewId" BIGSERIAL PRIMARY KEY,
    "InvoiceId" UUID NOT NULL REFERENCES public."Invoices"("InvoiceId"),
    "AssignedAuditorId" VARCHAR(50) NULL REFERENCES public."SystemUsers"("UserId"),
    "CalculatedRiskScore" INT NOT NULL,
    "TriggeredFlags" TEXT NULL,
    "AuditorDecision" VARCHAR(30) NULL,
    "DecisionReason" VARCHAR(255) NULL,
    "ResolvedAt" TIMESTAMPTZ NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.11 ترويسات ومطابقات أسماء المتاجر في الفواتير (Store Aliases)
CREATE TABLE IF NOT EXISTS public."StoreAliases" (
    "Id" SERIAL PRIMARY KEY,
    "TenantId" VARCHAR(50) NOT NULL REFERENCES public."Tenants"("TenantId") ON DELETE CASCADE,
    "AliasName" VARCHAR(200) NOT NULL,
    "MatchType" VARCHAR(30) DEFAULT 'CONTAINS',
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.12 سجل بصمات الأجهزة لمنع التحايل وتعدد الحسابات (Customer Devices)
CREATE TABLE IF NOT EXISTS public."CustomerDevices" (
    "Id" BIGSERIAL PRIMARY KEY,
    "CustomerId" VARCHAR(50) NOT NULL REFERENCES public."Customers"("CustomerId") ON DELETE CASCADE,
    "DeviceFingerprint" VARCHAR(150) NOT NULL,
    "DeviceModel" VARCHAR(100) NULL,
    "OsVersion" VARCHAR(50) NULL,
    "IpAddress" VARCHAR(50) NULL,
    "FirstSeenAt" TIMESTAMPTZ DEFAULT clock_timestamp(),
    "LastSeenAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.13 قواعد محفزات المنتجات ومضاعفة النقاط (Product Booster Rules)
CREATE TABLE IF NOT EXISTS public."ProductBoosterRules" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" VARCHAR(200) NOT NULL,
    "BrandOrItemKeyword" VARCHAR(200) NOT NULL,
    "TargetCategory" VARCHAR(50) NULL,
    "TenantId" VARCHAR(50) NULL REFERENCES public."Tenants"("TenantId") ON DELETE SET NULL,
    "BonusPoints" INT DEFAULT 20,
    "PointsMultiplier" NUMERIC(5, 2) DEFAULT 1.00,
    "SponsorName" VARCHAR(200) NULL,
    "StartDate" TIMESTAMPTZ NOT NULL,
    "EndDate" TIMESTAMPTZ NOT NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.14 قواعد احتساب النقاط المرنة (Flexible Loyalty Earning Rules)
CREATE TABLE IF NOT EXISTS public."LoyaltyEarningRules" (
    "RuleId" SERIAL PRIMARY KEY,
    "StoreTenantId" VARCHAR(50) NULL REFERENCES public."Tenants"("TenantId") ON DELETE SET NULL,
    "RuleName" VARCHAR(200) NOT NULL,
    "CalculationType" VARCHAR(50) NOT NULL DEFAULT 'SPEND_RATIO',
    "FixedPointsPerInvoice" INT DEFAULT 0,
    "SpendRatioRate" NUMERIC(5, 4) DEFAULT 0.1000,
    "MinInvoiceAmount" NUMERIC(18, 2) DEFAULT 0.00,
    "MaxPointsCap" INT NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp(),
    "UpdatedAt" TIMESTAMPTZ NULL
);

-- 2.15 الجوائز والمكافآت (Rewards)
CREATE TABLE IF NOT EXISTS public."Rewards" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" VARCHAR(150) NOT NULL,
    "Description" VARCHAR(500) NOT NULL,
    "MerchantName" VARCHAR(150) NULL,
    "PointsCost" INT NOT NULL,
    "DiscountValue" NUMERIC(18, 2) NOT NULL,
    "ImageUrl" VARCHAR(500) NULL,
    "StockQuantity" INT NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "ValidityDays" INT DEFAULT 30,
    "ValidUntil" TIMESTAMPTZ NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.16 استبدال الجوائز والقسائم (Reward Redemptions)
CREATE TABLE IF NOT EXISTS public."RewardRedemptions" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CustomerId" VARCHAR(50) NOT NULL REFERENCES public."Customers"("CustomerId"),
    "RewardId" UUID NOT NULL REFERENCES public."Rewards"("Id"),
    "RedemptionCode" VARCHAR(50) NOT NULL UNIQUE,
    "PointsSpent" INT NOT NULL,
    "Status" VARCHAR(50) NOT NULL DEFAULT 'ACTIVE',
    "RedeemedAt" TIMESTAMPTZ DEFAULT clock_timestamp(),
    "UsedAt" TIMESTAMPTZ NULL,
    "UsedByMerchant" VARCHAR(200) NULL,
    "ExpiresAt" TIMESTAMPTZ NOT NULL
);

-- 2.17 بانرات العروض والتسويق (Promotion Banners)
CREATE TABLE IF NOT EXISTS public."PromotionBanners" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" VARCHAR(200) NOT NULL,
    "Subtitle" VARCHAR(300) NULL,
    "ImageUrl" VARCHAR(500) NOT NULL,
    "TargetUrl" VARCHAR(500) NULL,
    "ActionType" VARCHAR(50) DEFAULT 'EXTERNAL_URL',
    "ActionPayloadJson" TEXT NULL,
    "TargetAudience" VARCHAR(50) DEFAULT 'ALL',
    "DisplayOrder" INT DEFAULT 0,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "StartDate" TIMESTAMPTZ NULL,
    "EndDate" TIMESTAMPTZ NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.18 حملات الولاء والمواسم (Loyalty Campaigns)
CREATE TABLE IF NOT EXISTS public."LoyaltyCampaigns" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" VARCHAR(200) NOT NULL,
    "Description" VARCHAR(500) NOT NULL,
    "PointsMultiplier" NUMERIC(4, 2) NOT NULL,
    "MinSpendAmount" NUMERIC(18, 2) NOT NULL,
    "StartDate" TIMESTAMPTZ NOT NULL,
    "EndDate" TIMESTAMPTZ NOT NULL,
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- 2.19 سجل تدقيق العمليات الأمنية (Audit Logs)
CREATE TABLE IF NOT EXISTS public."AuditLogs" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PerformedBy" VARCHAR(150) NOT NULL,
    "Action" VARCHAR(100) NOT NULL,
    "EntityName" VARCHAR(100) NOT NULL,
    "EntityId" VARCHAR(100) NULL,
    "Details" VARCHAR(1000) NOT NULL,
    "IpAddress" VARCHAR(50) NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT clock_timestamp()
);

-- ==============================================================================
-- 3. DIRECT MOBILE APP TABLES & REALTIME (جداول التطبيق المباشرة والتحديث الفوري)
-- ==============================================================================

-- 3.1 جدول المتاجر السريع للتطبيق (stores)
CREATE TABLE IF NOT EXISTS public.stores (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    logo TEXT NOT NULL,
    category TEXT NOT NULL,
    description TEXT NOT NULL,
    current_stamps_default INT DEFAULT 0,
    max_stamps INT NOT NULL DEFAULT 10,
    reward_text TEXT NOT NULL,
    moq TEXT NOT NULL,
    unit_location TEXT NOT NULL,
    verified BOOLEAN DEFAULT TRUE,
    commission_rate NUMERIC(5,2) DEFAULT 0.05,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3.2 جدول المتسوقات السريع للتطبيق (profiles)
CREATE TABLE IF NOT EXISTS public.profiles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    card_id TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    phone TEXT UNIQUE NOT NULL,
    email TEXT,
    points INT DEFAULT 0,
    stamps INT DEFAULT 0,
    prizes INT DEFAULT 0,
    birthdate DATE,
    gender TEXT DEFAULT 'أنثى',
    city TEXT DEFAULT 'طرابلس',
    avatar_url TEXT,
    tier TEXT DEFAULT 'العضوية الماسية VIP',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3.3 جدول مسح الفواتير ومكافحة التزوير (invoices_scans)
CREATE TABLE IF NOT EXISTS public.invoices_scans (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    scan_code TEXT UNIQUE NOT NULL,
    customer_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
    customer_name TEXT NOT NULL,
    store_id TEXT REFERENCES public.stores(id) ON DELETE CASCADE,
    store_name TEXT NOT NULL,
    invoice_no TEXT NOT NULL,
    invoice_date TIMESTAMPTZ DEFAULT NOW(),
    amount NUMERIC(10,2) NOT NULL,
    items JSONB DEFAULT '[]'::JSONB,
    status TEXT NOT NULL DEFAULT 'Approved' CHECK (status IN ('Approved', 'Pending', 'Flagged', 'Rejected')),
    fraud_risk TEXT NOT NULL DEFAULT 'Low' CHECK (fraud_risk IN ('Low', 'Medium', 'High')),
    phash_match TEXT,
    stamps_awarded INT DEFAULT 1,
    points_earned INT DEFAULT 100,
    image_url TEXT,
    ocr_confidence NUMERIC(4,2) DEFAULT 0.98,
    verified_by TEXT DEFAULT 'Gemini AI Vision',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3.4 سجل حركات الطوابع (loyalty_stamps_history)
CREATE TABLE IF NOT EXISTS public.loyalty_stamps_history (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
    store_id TEXT REFERENCES public.stores(id) ON DELETE CASCADE,
    scan_id UUID REFERENCES public.invoices_scans(id) ON DELETE SET NULL,
    amount_text TEXT NOT NULL,
    stamp_count INT NOT NULL DEFAULT 1,
    awarded_date TIMESTAMPTZ DEFAULT NOW()
);

-- 3.5 الإشعارات والتنبيهات (notifications)
CREATE TABLE IF NOT EXISTS public.notifications (
    id BIGSERIAL PRIMARY KEY,
    customer_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    message TEXT NOT NULL,
    section TEXT NOT NULL DEFAULT 'عام',
    logo TEXT NOT NULL DEFAULT '🦋',
    is_read BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 3.6 إعدادات المنظومة والذكاء الاصطناعي (system_settings)
CREATE TABLE IF NOT EXISTS public.system_settings (
    key TEXT PRIMARY KEY,
    value JSONB NOT NULL,
    description TEXT,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- ==============================================================================
-- 4. SEED DATA (إدراج المتاجر الـ 9 الرسمية والمستخدمين المعتمدين)
-- ==============================================================================

-- 4.1 إدراج المتاجر الرسمية في جدول Tenants
INSERT INTO public."Tenants" ("TenantId", "NameAr", "NameEn", "UnitNumber", "Category", "LogoUrl", "IsActive")
VALUES
    ('TEN-CLEANING', 'مواد التنظيف الفاخرة والعناية المنزلية', 'Luxury Cleaning & Home Care', 'G-12', 'Detergents & Cleaning', '🧼', true),
    ('TEN-GROCERY', 'سوبرماركت المواد الغذائية والتموينية', 'Fresh Groceries & Supermarket', 'G-02', 'Supermarket & Groceries', '🛒', true),
    ('TEN-EVENING-DRESS', 'بوتيك فساتين السهرة والأزياء الراقية', 'Evening Dresses & Haute Couture', '201-A', 'Evening Dresses & Fashion', '👗', true),
    ('TEN-KADI-CAFE', 'كافي كادي (Kadi Café)', 'Kadi Specialty Coffee', 'G-08', 'Cafes & Coffee', '☕', true),
    ('TEN-WOMEN-FASHION', 'محل الملابس النسائية والأناقة', 'Elegance Women''s Fashion', '110-A', 'Women''s Fashion', '👚', true),
    ('TEN-KIDS-WEAR', 'متجر ملابس الأطفال (Kids Wear)', 'Little Stars Kids Wear', '115-B', 'Kids & Toys', '🧸', true),
    ('TEN-CHOCOLATE', 'قصر الشكلاطة والحلويات الفاخرة', 'Royal Chocolatier House', 'G-15', 'Sweets & Chocolates', '🍫', true),
    ('TEN-ROYAL-TOUCH', 'مطعم اللمسة الملكية للمأكولات', 'Royal Touch Fine Dining', 'FC-01', 'Restaurants & Dining', '🍽️', true),
    ('TEN-SAQR-GAMES', 'محل صقر للألعاب والترفيه الإلكتروني', 'Saqr Gaming & Electronics', '208-B', 'Gaming & Entertainment', '🎮', true)
ON CONFLICT ("TenantId") DO UPDATE SET "NameAr" = EXCLUDED."NameAr", "NameEn" = EXCLUDED."NameEn";

-- 4.2 إدراج المتاجر في جدول stores (للتطبيق)
INSERT INTO public.stores (id, name, logo, category, description, max_stamps, reward_text, moq, unit_location, verified)
VALUES
    ('TEN-CLEANING', 'مواد التنظيف الفاخرة والعناية المنزلية', '🧼', 'مواد تنظيف وعناية', 'تجميع 10 طوابع للحصول على باقة منظفات فاخرة مجاناً', 10, 'باقة منظفات وعناية بقيمة 120 د.ل', '5 قطع', 'الوحدة G-12', true),
    ('TEN-GROCERY', 'سوبرماركت المواد الغذائية والتموينية', '🛒', 'مواد غذائية وتموين', 'تجميع 8 طوابع للحصول على قسيمة مشتريات غذائية', 8, 'قسيمة بقيمة 100 د.ل من قسم الأغذية', '10 عبوات', 'الوحدة G-02', true),
    ('TEN-EVENING-DRESS', 'بوتيك فساتين السهرة والأزياء الراقية', '👗', 'فساتين سهرة وأزياء', 'تجميع 10 طوابع للحصول على فستان سهرة مخفض بنسبة 50%', 10, 'قسيمة خصم 250 د.ل على فساتين السهرة', 'قطعة واحدة', 'الوحدة 201-A', true),
    ('TEN-KADI-CAFE', 'كافي كادي (Kadi Café)', '☕', 'مقاهي ومشروبات', 'تجميع 6 طوابع للحصول على مشروب وحلوى مجاناً', 6, 'وجبة فطور وقهوة مختصة مجاناً', 'مشروب واحد', 'الوحدة G-08', true),
    ('TEN-WOMEN-FASHION', 'محل الملابس النسائية والأناقة', '👚', 'ملابس نسائية', 'تجميع 10 طوابع للحصول على طقم ملابس نسائي', 10, 'قسيمة بقيمة 150 د.ل على الأزياء النسائية', 'قطعتين', 'الوحدة 110-A', true),
    ('TEN-KIDS-WEAR', 'متجر ملابس الأطفال (Kids Wear)', '🧸', 'ملابس وألعاب أطفال', 'تجميع 8 طوابع للحصول على طقم أطفال كامل', 8, 'طقم أطفال كامل بقيمة 120 د.ل', '3 قطع', 'الوحدة 115-B', true),
    ('TEN-CHOCOLATE', 'قصر الشكلاطة والحلويات الفاخرة', '🍫', 'شوكولاتة وحلويات', 'تجميع 8 طوابع للحصول على صندوق شوكولاتة بلجيكية فاخرة', 8, 'صندوق شوكولاتة مشكل 1 كجم', 'علبة واحدة', 'الوحدة G-15', true),
    ('TEN-ROYAL-TOUCH', 'مطعم اللمسة الملكية للمأكولات', '🍽️', 'مطاعم ومأكولات', 'تجميع 10 طوابع للحصول على عشاء عائلي مجاني', 10, 'وجبة عشاء ملكية بقيمة 180 د.ل', 'وجبة واحدة', 'الوحدة FC-01', true),
    ('TEN-SAQR-GAMES', 'محل صقر للألعاب والترفيه الإلكتروني', '🎮', 'ألعاب وإلكترونيات', 'تجميع 12 طابع للحصول على جهاز ألعاب أو إكسسوار', 12, 'يد تحكم لاسلكية أو لعبة مجانية', 'قطعة واحدة', 'الوحدة 208-B', true)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, logo = EXCLUDED.logo, category = EXCLUDED.category;

-- 4.3 إدراج المستخدمين والإدارة (المبرمج، مصطفى صوان، مراد)
-- ملاحظة: كلمات المرور أدناه ليست بصيغة التطبيق ولا يمكن الدخول بها.
-- عند أول تشغيل للـ API بدون مدير عام صالح يُنشأ تلقائياً الحساب "admin" بكلمة مرور تظهر في سجل الخادم.
-- (الـ API ينشئ ويُكمل الجداول تلقائياً، فتشغيل هذا الملف لم يعد ضرورياً)
INSERT INTO public."SystemUsers" ("UserId", "TenantId", "FullName", "Email", "PhoneNumber", "PasswordHash", "Role", "IsActive")
VALUES
    ('programer', null, 'المطور والمسؤول التقني العام للنظام (Programmer)', 'dev@mmapp.ly', '0900000099', 'jZae727K08KaOmKSgOaGzww/XVqGr/hqq551Wkw5qnw=', 'SUPER_ADMIN', true),
    ('USR-MOSTAFA', null, 'مصطفى صوان (المدير العام للمول)', 'mostafa.sawan@mmapp.ly', '0910000001', 'jZae727K08KaOmKSgOaGzww/XVqGr/hqq551Wkw5qnw=', 'SUPER_ADMIN', true),
    ('USR-MOURAD', null, 'مراد المنفي (مدقق الفواتير والاحتيال)', 'mourad@mmapp.ly', '0910000002', 'jZae727K08KaOmKSgOaGzww/XVqGr/hqq551Wkw5qnw=', 'FRAUD_AUDITOR', true)
ON CONFLICT ("UserId") DO UPDATE SET "FullName" = EXCLUDED."FullName", "Role" = EXCLUDED."Role";

-- 4.4 إدراج بيانات العملاء وبطاقة الولاء
INSERT INTO public."Customers" ("CustomerId", "FullName", "PhoneNumber", "LoyaltyTier", "IsFrozen")
VALUES ('CUST-001', 'منى محمد الزنتاني', '0912345678', 'VIP', false)
ON CONFLICT ("CustomerId") DO NOTHING;

INSERT INTO public.profiles (card_id, name, phone, email, points, stamps, prizes, city, tier)
VALUES ('CUST-001', 'منى محمد الزنتاني', '0912345678', 'mouna@mmapp.ly', 15400, 4, 2, 'طرابلس', 'العضوية الماسية VIP')
ON CONFLICT (phone) DO UPDATE SET name = EXCLUDED.name, points = EXCLUDED.points;

-- 4.5 إدراج إشعارات ترحيبية
INSERT INTO public.notifications (title, message, section, logo, is_read)
VALUES
    ('مرحباً بكِ في المرأة مول 🦋', 'تم تفعيل بطاقة العضوية الخاصة بك بنجاح مع 15,400 نقطة ولاء افتتاحية.', 'عام', '🦋', false),
    ('طوابع مضافة - مواد التنظيف الفاخرة 🧼', 'تم مسح فاتورة بقيمة 320 د.ل واعتماد 2 طوابع في رصيد بطاقتك بنجاح.', 'المتاجر', '🧼', false),
    ('عرض كافي كادي الحصري ☕', 'احصلي على قهوة وقطعة حلوى مجانية عند جمع 6 طوابع فقط في كافي كادي.', 'العروض', '☕', true);

-- 4.6 إعدادات المنظومة
INSERT INTO public.system_settings (key, value, description)
VALUES
    ('ai_ocr_threshold', '{"min_confidence": 0.85, "auto_approve": true, "model": "gemini-2.5-flash"}'::JSONB, 'إعدادات الثقة للذكاء الاصطناعي'),
    ('poisson_analytics', '{"lambda": 38.5, "peak_hour": "20:00", "window_minutes": 60}'::JSONB, 'معدل تدفق عمليات المتسوقين في الدقيقة'),
    ('fraud_rules', '{"phash_hamming_max": 10, "max_scans_per_hour": 5, "anti_sybil": true}'::JSONB, 'قواعد مكافحة الاحتيال والتكرار')
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value;

-- ==============================================================================
-- 5. REALTIME SUBSCRIPTIONS & PERMISSIONS
-- ==============================================================================

DO $$$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'invoices_scans') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public.invoices_scans;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'profiles') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public.profiles;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'stores') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public.stores;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'notifications') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public.notifications;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'Invoices') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public."Invoices";
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'CustomerWallets') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE public."CustomerWallets";
    END IF;
END $$$;

-- منح الصلاحيات الشاملة لكافة الجداول والـ Schemas
GRANT USAGE ON SCHEMA public TO anon, authenticated, service_role;
GRANT ALL ON ALL TABLES IN SCHEMA public TO anon, authenticated, service_role;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO anon, authenticated, service_role;
GRANT ALL ON ALL ROUTINES IN SCHEMA public TO anon, authenticated, service_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO anon, authenticated, service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO anon, authenticated, service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON ROUTINES TO anon, authenticated, service_role;
