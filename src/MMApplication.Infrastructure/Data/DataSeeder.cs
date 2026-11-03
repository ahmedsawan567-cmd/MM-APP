using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MMApplication.Application.Interfaces;
using MMApplication.Domain.Entities;
using MMApplication.Domain.Enums;

namespace MMApplication.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context, IPasswordHasherService passwordHasher)
    {
        var now = DateTime.UtcNow;

        // ----------------------------------------------------
        // 1. 🏢 TABLE: Tenants (المتاجر والمحلات الرسمية في المول)
        // ----------------------------------------------------
        var tenants = new List<Tenant>
        {
            // 1. مواد التنظيف
            new()
            {
                TenantId = "TEN-CLEANING",
                NameAr = "مواد التنظيف الفاخرة والعناية المنزلية",
                NameEn = "Luxury Cleaning & Home Care",
                UnitNumber = "G-12",
                Category = "Detergents & Cleaning",
                LogoUrl = "https://images.unsplash.com/photo-1585421514738-01798e348b17?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            // 2. المواد الغذائية
            new()
            {
                TenantId = "TEN-GROCERY",
                NameAr = "سوبرماركت المواد الغذائية والتموينية",
                NameEn = "Fresh Groceries & Supermarket",
                UnitNumber = "G-02",
                Category = "Supermarket & Groceries",
                LogoUrl = "https://images.unsplash.com/photo-1578916171728-46686eac8d58?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-8)
            },
            // 3. فساتين السهرة
            new()
            {
                TenantId = "TEN-EVENING-DRESS",
                NameAr = "بوتيك فساتين السهرة والأزياء الراقية",
                NameEn = "Evening Dresses & Haute Couture",
                UnitNumber = "201-A",
                Category = "Evening Dresses & Fashion",
                LogoUrl = "https://images.unsplash.com/photo-1566174053879-31528523f8ae?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-5)
            },
            // 4. كافي كادي
            new()
            {
                TenantId = "TEN-KADI-CAFE",
                NameAr = "كافي كادي (Kadi Café)",
                NameEn = "Kadi Specialty Coffee",
                UnitNumber = "G-08",
                Category = "Cafes & Coffee",
                LogoUrl = "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-7)
            },
            // 5. الملابس النسائية
            new()
            {
                TenantId = "TEN-WOMEN-FASHION",
                NameAr = "محل الملابس النسائية والأناقة",
                NameEn = "Elegance Women's Fashion",
                UnitNumber = "110-A",
                Category = "Women's Fashion",
                LogoUrl = "https://images.unsplash.com/photo-1489987707025-afc232f7ea0f?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            // 6. ملابس الأطفال
            new()
            {
                TenantId = "TEN-KIDS-WEAR",
                NameAr = "متجر ملابس الأطفال (Kids Wear)",
                NameEn = "Little Stars Kids Wear",
                UnitNumber = "115-B",
                Category = "Kids & Toys",
                LogoUrl = "https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-5)
            },
            // 7. محل للشكلاطة
            new()
            {
                TenantId = "TEN-CHOCOLATE",
                NameAr = "قصر الشوكولاتة والحلويات الفاخرة",
                NameEn = "Royal Chocolatier House",
                UnitNumber = "G-15",
                Category = "Sweets & Chocolates",
                LogoUrl = "https://images.unsplash.com/photo-1548907040-4baa42d10919?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-4)
            },
            // 8. مطعم اللمسة الملكية
            new()
            {
                TenantId = "TEN-ROYAL-TOUCH",
                NameAr = "مطعم اللمسة الملكية للمأكولات",
                NameEn = "Royal Touch Fine Dining",
                UnitNumber = "FC-01",
                Category = "Restaurants & Dining",
                LogoUrl = "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            // 9. محل صقر للألعاب
            new()
            {
                TenantId = "TEN-SAQR-GAMES",
                NameAr = "محل صقر للألعاب والترفيه الإلكتروني",
                NameEn = "Saqr Gaming & Electronics",
                UnitNumber = "208-B",
                Category = "Gaming & Entertainment",
                LogoUrl = "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-5)
            },
            // Additional major brands
            new()
            {
                TenantId = "TEN-ZARA",
                NameAr = "Zara - زارا للأزياء",
                NameEn = "Zara Fashion",
                UnitNumber = "101-A",
                Category = "Fashion",
                LogoUrl = "https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-8)
            },
            new()
            {
                TenantId = "TEN-STARBUCKS",
                NameAr = "Starbucks Coffee - ستاربكس كافيه",
                NameEn = "Starbucks Coffee",
                UnitNumber = "G-04",
                Category = "Cafes & Coffee",
                LogoUrl = "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-8)
            },
            new()
            {
                TenantId = "TEN-SEPHORA",
                NameAr = "Sephora - سيفورا لمستحضرات التجميل",
                NameEn = "Sephora Cosmetics",
                UnitNumber = "112-B",
                Category = "Cosmetics",
                LogoUrl = "https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-7)
            },
            new()
            {
                TenantId = "TEN-APPLE",
                NameAr = "iStore - موزع أبل المعتمد",
                NameEn = "iStore Apple Reseller",
                UnitNumber = "205-A",
                Category = "Electronics",
                LogoUrl = "https://images.unsplash.com/photo-1519389950473-47ba0277781c?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            new()
            {
                TenantId = "TEN-NIKE",
                NameAr = "Nike Store - متجر نايكي الرياضي",
                NameEn = "Nike Official Store",
                UnitNumber = "120",
                Category = "Fashion",
                LogoUrl = "https://images.unsplash.com/photo-1542291026-7eec264c27ff?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            new()
            {
                TenantId = "TEN-CARREFOUR",
                NameAr = "Carrefour Market - هايبرماركت كارفور",
                NameEn = "Carrefour Hypermarket",
                UnitNumber = "G-01",
                Category = "Supermarket & Groceries",
                LogoUrl = "https://images.unsplash.com/photo-1578916171728-46686eac8d58?w=200&h=200&fit=crop",
                IsActive = true,
                CreatedAt = now.AddMonths(-8)
            }
        };

        foreach (var t in tenants)
        {
            if (!await context.Tenants.AnyAsync(x => x.TenantId == t.TenantId))
            {
                await context.Tenants.AddAsync(t);
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 2. ⚙️ TABLE: TenantLoyaltyRules (قواعد احتساب النقاط والمكافآت)
        // ----------------------------------------------------
        var rules = new List<TenantLoyaltyRule>
        {
            new()
            {
                TenantId = "TEN-CLEANING",
                RewardType = "POINTS",
                ConversionRate = 0.1000m, // 1 point per 10 LYD
                MinInvoiceTotal = 25.00m,
                MaxPointsCap = 250,
                PointsExpiryDays = 180,
                MaxInvoiceAgeDays = 7,
                DynamicRulesJson = "{\"bulkCleanerBonus\": 1.25}",
                UpdatedAt = now.AddMonths(-5)
            },
            new()
            {
                TenantId = "TEN-GROCERY",
                RewardType = "POINTS",
                ConversionRate = 0.0800m,
                MinInvoiceTotal = 30.00m,
                MaxPointsCap = 300,
                PointsExpiryDays = 90,
                MaxInvoiceAgeDays = 5,
                DynamicRulesJson = "{\"weekendGroceries\": 1.3}",
                UpdatedAt = now.AddMonths(-6)
            },
            new()
            {
                TenantId = "TEN-EVENING-DRESS",
                RewardType = "POINTS",
                ConversionRate = 0.1500m,
                MinInvoiceTotal = 150.00m,
                MaxPointsCap = 1500,
                PointsExpiryDays = 365,
                MaxInvoiceAgeDays = 14,
                DynamicRulesJson = "{\"vipEveningBonus\": 2.0, \"hauteCouture\": 1.5}",
                UpdatedAt = now.AddMonths(-4)
            },
            new()
            {
                TenantId = "TEN-KADI-CAFE",
                RewardType = "CASHBACK",
                ConversionRate = 0.1000m, // 10% cashback
                MinInvoiceTotal = 10.00m,
                MaxPointsCap = 100,
                PointsExpiryDays = 60,
                MaxInvoiceAgeDays = 3,
                DynamicRulesJson = "{\"morningCoffeeBonus\": 1.25}",
                UpdatedAt = now.AddMonths(-6)
            },
            new()
            {
                TenantId = "TEN-WOMEN-FASHION",
                RewardType = "POINTS",
                ConversionRate = 0.1200m,
                MinInvoiceTotal = 60.00m,
                MaxPointsCap = 600,
                PointsExpiryDays = 180,
                MaxInvoiceAgeDays = 7,
                DynamicRulesJson = "{\"seasonLaunchBonus\": 1.4}",
                UpdatedAt = now.AddMonths(-5)
            },
            new()
            {
                TenantId = "TEN-KIDS-WEAR",
                RewardType = "POINTS",
                ConversionRate = 0.1000m,
                MinInvoiceTotal = 50.00m,
                MaxPointsCap = 400,
                PointsExpiryDays = 180,
                MaxInvoiceAgeDays = 7,
                DynamicRulesJson = "{\"eidKidsBonus\": 1.5}",
                UpdatedAt = now.AddMonths(-4)
            },
            new()
            {
                TenantId = "TEN-CHOCOLATE",
                RewardType = "CASHBACK",
                ConversionRate = 0.0800m, // 8% cashback
                MinInvoiceTotal = 20.00m,
                MaxPointsCap = 150,
                PointsExpiryDays = 90,
                MaxInvoiceAgeDays = 5,
                DynamicRulesJson = "{\"chocolateLoversBonus\": 1.2}",
                UpdatedAt = now.AddMonths(-3)
            },
            new()
            {
                TenantId = "TEN-ROYAL-TOUCH",
                RewardType = "CASHBACK",
                ConversionRate = 0.0500m, // 5% cashback
                MinInvoiceTotal = 40.00m,
                MaxPointsCap = 200,
                PointsExpiryDays = 90,
                MaxInvoiceAgeDays = 4,
                DynamicRulesJson = "{\"familyDinnerBonus\": 1.2}",
                UpdatedAt = now.AddMonths(-5)
            },
            new()
            {
                TenantId = "TEN-SAQR-GAMES",
                RewardType = "POINTS",
                ConversionRate = 0.1000m,
                MinInvoiceTotal = 30.00m,
                MaxPointsCap = 500,
                PointsExpiryDays = 180,
                MaxInvoiceAgeDays = 7,
                DynamicRulesJson = "{\"gamersWeekendMultiplier\": 1.5}",
                UpdatedAt = now.AddMonths(-4)
            },
            new()
            {
                TenantId = "TEN-ZARA",
                RewardType = "POINTS",
                ConversionRate = 0.1000m,
                MinInvoiceTotal = 50.00m,
                MaxPointsCap = 500,
                PointsExpiryDays = 180,
                MaxInvoiceAgeDays = 7,
                DynamicRulesJson = "{\"fridayMultiplier\": 1.5}",
                UpdatedAt = now.AddMonths(-6)
            },
            new()
            {
                TenantId = "TEN-STARBUCKS",
                RewardType = "CASHBACK",
                ConversionRate = 0.0500m,
                MinInvoiceTotal = 15.00m,
                MaxPointsCap = 100,
                PointsExpiryDays = 90,
                MaxInvoiceAgeDays = 3,
                DynamicRulesJson = "{\"morningBonus\": 1.2}",
                UpdatedAt = now.AddMonths(-6)
            },
            new()
            {
                TenantId = "TEN-SEPHORA",
                RewardType = "POINTS",
                ConversionRate = 0.1500m,
                MinInvoiceTotal = 100.00m,
                MaxPointsCap = 1000,
                PointsExpiryDays = 365,
                MaxInvoiceAgeDays = 14,
                DynamicRulesJson = "{\"vipBonus\": 2.0}",
                UpdatedAt = now.AddMonths(-5)
            },
            new()
            {
                TenantId = "TEN-APPLE",
                RewardType = "POINTS",
                ConversionRate = 0.0500m,
                MinInvoiceTotal = 500.00m,
                MaxPointsCap = 2000,
                PointsExpiryDays = 365,
                MaxInvoiceAgeDays = 30,
                DynamicRulesJson = "{\"accessoryBonus\": 1.5}",
                UpdatedAt = now.AddMonths(-4)
            }
        };

        foreach (var r in rules)
        {
            if (!await context.TenantLoyaltyRules.AnyAsync(x => x.TenantId == r.TenantId))
            {
                await context.TenantLoyaltyRules.AddAsync(r);
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 3. 🏷️ TABLE: StoreAliases (ترويسات ومطابقات الـ OCR الآلية)
        // ----------------------------------------------------
        var aliases = new List<StoreAlias>
        {
            // مواد التنظيف
            new() { TenantId = "TEN-CLEANING", AliasName = "مواد التنظيف", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-CLEANING", AliasName = "مجمع المنظفات", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-CLEANING", AliasName = "Detergents", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-CLEANING", AliasName = "Clean Home", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },

            // المواد الغذائية
            new() { TenantId = "TEN-GROCERY", AliasName = "المواد الغذائية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-GROCERY", AliasName = "سوبرماركت المواد الغذائية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-GROCERY", AliasName = "Food Market", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-GROCERY", AliasName = "Grocery", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },

            // فساتين السهرة
            new() { TenantId = "TEN-EVENING-DRESS", AliasName = "فساتين السهرة", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-EVENING-DRESS", AliasName = "بوتيك فساتين السهرة", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-EVENING-DRESS", AliasName = "Evening Dresses", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-EVENING-DRESS", AliasName = "Haute Couture", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },

            // كافي كادي
            new() { TenantId = "TEN-KADI-CAFE", AliasName = "كافي كادي", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-KADI-CAFE", AliasName = "كافيه كادي", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-KADI-CAFE", AliasName = "Kadi Cafe", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-KADI-CAFE", AliasName = "Caffe Kadi", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-KADI-CAFE", AliasName = "مقهى كادي", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },

            // الملابس النسائية
            new() { TenantId = "TEN-WOMEN-FASHION", AliasName = "الملابس النسائية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-WOMEN-FASHION", AliasName = "بوتيك الملابس النسائية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-WOMEN-FASHION", AliasName = "Women Fashion", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-WOMEN-FASHION", AliasName = "أزياء نسائية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },

            // ملابس الأطفال
            new() { TenantId = "TEN-KIDS-WEAR", AliasName = "ملابس الأطفال", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-KIDS-WEAR", AliasName = "متجر ملابس الأطفال", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-KIDS-WEAR", AliasName = "Kids Wear", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-KIDS-WEAR", AliasName = "أطفال كيدز", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },

            // محل للشكلاطة
            new() { TenantId = "TEN-CHOCOLATE", AliasName = "محل للشكلاطة", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-3) },
            new() { TenantId = "TEN-CHOCOLATE", AliasName = "محل الشكلاطة", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-3) },
            new() { TenantId = "TEN-CHOCOLATE", AliasName = "قصر الشوكولاتة", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-3) },
            new() { TenantId = "TEN-CHOCOLATE", AliasName = "قصر الشكلاطة", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-3) },
            new() { TenantId = "TEN-CHOCOLATE", AliasName = "Chocolatier", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-3) },

            // مطعم اللمسة الملكية
            new() { TenantId = "TEN-ROYAL-TOUCH", AliasName = "مطعم اللمسة الملكية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-ROYAL-TOUCH", AliasName = "اللمسة الملكية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-ROYAL-TOUCH", AliasName = "Royal Touch Restaurant", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-ROYAL-TOUCH", AliasName = "مطاعم اللمسة الملكية", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },

            // محل صقر للألعاب
            new() { TenantId = "TEN-SAQR-GAMES", AliasName = "محل صقر للألعاب", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-SAQR-GAMES", AliasName = "صقر للألعاب", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-SAQR-GAMES", AliasName = "Saqr Gaming & Toys", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },
            new() { TenantId = "TEN-SAQR-GAMES", AliasName = "Saqr Games", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-4) },

            // البراندات السابقة
            new() { TenantId = "TEN-ZARA", AliasName = "Zara", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-7) },
            new() { TenantId = "TEN-ZARA", AliasName = "زارا للأزياء", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-7) },
            new() { TenantId = "TEN-STARBUCKS", AliasName = "Starbucks", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-7) },
            new() { TenantId = "TEN-STARBUCKS", AliasName = "ستاربكس كافيه", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-7) },
            new() { TenantId = "TEN-SEPHORA", AliasName = "Sephora", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-SEPHORA", AliasName = "سيفورا", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-APPLE", AliasName = "iStore", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-APPLE", AliasName = "موزع أبل المعتمد", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-6) },
            new() { TenantId = "TEN-NIKE", AliasName = "Nike", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-5) },
            new() { TenantId = "TEN-CARREFOUR", AliasName = "Carrefour", MatchType = "CONTAINS", IsActive = true, CreatedAt = now.AddMonths(-7) }
        };

        foreach (var alias in aliases)
        {
            if (!await context.StoreAliases.AnyAsync(x => x.TenantId == alias.TenantId && x.AliasName == alias.AliasName))
            {
                await context.StoreAliases.AddAsync(alias);
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 4. 👥 TABLE: SystemUsers (المستخدمون، المدراء، والموظفون المطلوبون)
        // ----------------------------------------------------
        var devPw = passwordHasher.HashPassword("Programmer123!");
        var adminPw = passwordHasher.HashPassword("Admin123!");
        var auditorPw = passwordHasher.HashPassword("Auditor123!");
        var managerPw = passwordHasher.HashPassword("Manager123!");
        var cashierPw = passwordHasher.HashPassword("Merchant123!");

        var systemUsers = new List<SystemUser>
        {
            // 1. يوزر programer (المطور والمسؤول العام)
            new()
            {
                UserId = "programer",
                TenantId = null,
                FullName = "المطور والمسؤول العام للنظام (Programmer)",
                Email = "dev@mmapp.ly",
                PhoneNumber = "0900000099",
                PasswordHash = devPw,
                Role = SystemUserRole.SUPER_ADMIN,
                IsActive = true,
                CreatedAt = now.AddYears(-1)
            },
            // 2. مصطفى صوان (المدير العام)
            new()
            {
                UserId = "USR-MOSTAFA",
                TenantId = null,
                FullName = "مصطفى صوان (المدير العام للمول)",
                Email = "mostafa.sawan@mmapp.ly",
                PhoneNumber = "0910000001",
                PasswordHash = adminPw,
                Role = SystemUserRole.SUPER_ADMIN,
                IsActive = true,
                CreatedAt = now.AddYears(-1)
            },
            // 3. مراد (موظف ومدقق الفواتير والاحتيال)
            new()
            {
                UserId = "USR-MOURAD",
                TenantId = null,
                FullName = "مراد المنفي (مدقق الفواتير والاحتيال)",
                Email = "mourad@mmapp.ly",
                PhoneNumber = "0910000002",
                PasswordHash = auditorPw,
                Role = SystemUserRole.FRAUD_AUDITOR,
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            // 4. مريم الصالح (إدارة المول)
            new()
            {
                UserId = "USR-SUPERADMIN",
                TenantId = null,
                FullName = "مريم الصالح (نائب المدير العام)",
                Email = "admin@mmapp.ly",
                PhoneNumber = "0900000000",
                PasswordHash = adminPw,
                Role = SystemUserRole.SUPER_ADMIN,
                IsActive = true,
                CreatedAt = now.AddYears(-1)
            },
            // 5. سارة المنصوري (مدقق فواتير)
            new()
            {
                UserId = "USR-AUDITOR-01",
                TenantId = null,
                FullName = "سارة المنصوري (مدقق الفواتير والاحتيال)",
                Email = "sara.auditor@mmapp.ly",
                PhoneNumber = "0901111111",
                PasswordHash = auditorPw,
                Role = SystemUserRole.FRAUD_AUDITOR,
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            // 6. مدراء وكاشيرات المحلات
            new()
            {
                UserId = "USR-KADI-MGR",
                TenantId = "TEN-KADI-CAFE",
                FullName = "أحمد القمودي (مدير كافي كادي)",
                Email = "manager.kadi@mmapp.ly",
                PhoneNumber = "0915555001",
                PasswordHash = managerPw,
                Role = SystemUserRole.STORE_MANAGER,
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            new()
            {
                UserId = "USR-ROYAL-MGR",
                TenantId = "TEN-ROYAL-TOUCH",
                FullName = "كريم الهوني (مدير مطعم اللمسة الملكية)",
                Email = "manager.royal@mmapp.ly",
                PhoneNumber = "0915555002",
                PasswordHash = managerPw,
                Role = SystemUserRole.STORE_MANAGER,
                IsActive = true,
                CreatedAt = now.AddMonths(-5)
            },
            new()
            {
                UserId = "USR-SAQR-MGR",
                TenantId = "TEN-SAQR-GAMES",
                FullName = "صقر عبد الله (مدير محل صقر للألعاب)",
                Email = "manager.saqr@mmapp.ly",
                PhoneNumber = "0915555003",
                PasswordHash = managerPw,
                Role = SystemUserRole.STORE_MANAGER,
                IsActive = true,
                CreatedAt = now.AddMonths(-5)
            },
            new()
            {
                UserId = "USR-ZARA-MGR",
                TenantId = "TEN-ZARA",
                FullName = "طارق النعاس (مدير فرع زارا)",
                Email = "tariq.zara@mmapp.ly",
                PhoneNumber = "0902222222",
                PasswordHash = managerPw,
                Role = SystemUserRole.STORE_MANAGER,
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            },
            new()
            {
                UserId = "USR-ZARA-CSH",
                TenantId = "TEN-ZARA",
                FullName = "كاشير ونقاط البيع",
                Email = "cashier.zara@mmapp.ly",
                PhoneNumber = "0988888888",
                PasswordHash = cashierPw,
                Role = SystemUserRole.CASHIER,
                PinCodeHash = passwordHasher.HashPassword("1234"),
                IsActive = true,
                CreatedAt = now.AddMonths(-6)
            }
        };

        foreach (var u in systemUsers)
        {
            var existingUser = await context.SystemUsers.FirstOrDefaultAsync(x => x.UserId == u.UserId);
            if (existingUser == null)
            {
                await context.SystemUsers.AddAsync(u);
            }
            else
            {
                // Update properties in case of password re-seed
                existingUser.FullName = u.FullName;
                existingUser.Email = u.Email;
                existingUser.PhoneNumber = u.PhoneNumber;
                existingUser.PasswordHash = u.PasswordHash;
                existingUser.Role = u.Role;
                existingUser.IsActive = u.IsActive;
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 5. 🛍️ TABLE: Customers (المتسوقون في المول)
        // ----------------------------------------------------
        // كلمة مرور الحسابات التجريبية للمتسوقين (موثقة في README)
        var customerPw = passwordHasher.HashPassword("Customer123!");

        var customers = new List<Customer>
        {
            new()
            {
                CustomerId = "CUST-001",
                FullName = "أحمد محمد الزنتاني",
                PhoneNumber = "0912345678",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-SM-G998B-8831",
                LoyaltyTier = "SILVER",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-4)
            },
            new()
            {
                CustomerId = "CUST-002",
                FullName = "منال الشريف",
                PhoneNumber = "0911112233",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-IPHONE15-9921",
                LoyaltyTier = "GOLD",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-6)
            },
            new()
            {
                CustomerId = "CUST-003",
                FullName = "خالد الترهوني",
                PhoneNumber = "0925556677",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-XIAOMI13-7741",
                LoyaltyTier = "SILVER",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-3)
            },
            new()
            {
                CustomerId = "CUST-004",
                FullName = "عمر الفرجاني (حساب مجمد)",
                PhoneNumber = "0939998877",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-SUSP-9900",
                LoyaltyTier = "BRONZE",
                IsFrozen = true,
                RegisteredAt = now.AddMonths(-2)
            },
            new()
            {
                CustomerId = "CUST-005",
                FullName = "فاطمة الزهراء الورفلي",
                PhoneNumber = "0914445566",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-IPHONE14-1120",
                LoyaltyTier = "VIP",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-7)
            },
            new()
            {
                CustomerId = "CUST-006",
                FullName = "يوسف القرقني",
                PhoneNumber = "0923334455",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-SAMS23-4412",
                LoyaltyTier = "GOLD",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-5)
            },
            new()
            {
                CustomerId = "CUST-007",
                FullName = "آية المهدوي",
                PhoneNumber = "0917778899",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-HUAWEI-8832",
                LoyaltyTier = "SILVER",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-4)
            },
            new()
            {
                CustomerId = "CUST-008",
                FullName = "محمود السويحلي",
                PhoneNumber = "0941112233",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-OPPO-5510",
                LoyaltyTier = "BRONZE",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-1)
            },
            new()
            {
                CustomerId = "CUST-009",
                FullName = "مريم بن عيسى",
                PhoneNumber = "0928889900",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-IPHONE15PRO-0012",
                LoyaltyTier = "VIP",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-8)
            },
            new()
            {
                CustomerId = "CUST-010",
                FullName = "سالم الأوجلي",
                PhoneNumber = "0916667788",
                PasswordHash = customerPw,
                DeviceFingerprint = "DEV-HONOR-9081",
                LoyaltyTier = "GOLD",
                IsFrozen = false,
                RegisteredAt = now.AddMonths(-5)
            }
        };

        foreach (var c in customers)
        {
            if (!await context.Customers.AnyAsync(x => x.CustomerId == c.CustomerId))
            {
                await context.Customers.AddAsync(c);
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 6. 💳 TABLE: CustomerWallets (محافظ العملاء)
        // ----------------------------------------------------
        var wallets = new List<CustomerWallet>
        {
            new() { CustomerId = "CUST-001", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 150m },
            new() { CustomerId = "CUST-001", TenantId = "TEN-KADI-CAFE", BalanceType = BalanceType.CASHBACK, CurrentBalance = 18.50m },
            new() { CustomerId = "CUST-002", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 2450m },
            new() { CustomerId = "CUST-002", TenantId = "TEN-EVENING-DRESS", BalanceType = BalanceType.POINTS, CurrentBalance = 450m },
            new() { CustomerId = "CUST-003", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 920m },
            new() { CustomerId = "CUST-004", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 0.00m },
            new() { CustomerId = "CUST-005", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 6800m },
            new() { CustomerId = "CUST-005", TenantId = "TEN-ROYAL-TOUCH", BalanceType = BalanceType.CASHBACK, CurrentBalance = 42.00m },
            new() { CustomerId = "CUST-006", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 1950m },
            new() { CustomerId = "CUST-007", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 840m },
            new() { CustomerId = "CUST-008", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 180m },
            new() { CustomerId = "CUST-009", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 5400m },
            new() { CustomerId = "CUST-010", TenantId = null, BalanceType = BalanceType.POINTS, CurrentBalance = 2100m }
        };

        foreach (var w in wallets)
        {
            if (!await context.CustomerWallets.AnyAsync(x => x.CustomerId == w.CustomerId && x.TenantId == w.TenantId && x.BalanceType == w.BalanceType))
            {
                await context.CustomerWallets.AddAsync(w);
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 7. 🧾 TABLE: Invoices (فواتير واقعية للمحلات المطلوبة)
        // ----------------------------------------------------
        var invKadi = Guid.NewGuid();
        var invRoyal = Guid.NewGuid();
        var invDress = Guid.NewGuid();
        var invSaqr = Guid.NewGuid();
        var invChoc = Guid.NewGuid();
        var invCleaning = Guid.NewGuid();
        var invSuspect1 = Guid.NewGuid();
        var invSuspect2 = Guid.NewGuid();
        var invRejected1 = Guid.NewGuid();

        var seedInvoices = new List<Invoice>
        {
            // 1. كافي كادي (Approved)
            new()
            {
                InvoiceId = invKadi,
                CustomerId = "CUST-001",
                TenantId = "TEN-KADI-CAFE",
                ExtractedMerchant = "كافي كادي (Kadi Café)",
                InvoiceNumber = "INV-KD-5021",
                InvoiceDate = now.AddDays(-1).AddHours(-2),
                TotalAmount = 45.00m,
                ConfidenceScore = 0.9890m,
                RawAiPayload = "{\"merchant\": \"كافي كادي\", \"total\": 45.00, \"itemsCount\": 2}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?w=600&h=800&fit=crop",
                ImagePHash = "a1b2c3d4e5f60011",
                FraudRiskScore = 8,
                ProcessingStatus = InvoiceStatus.APPROVED,
                PointsAwarded = 0,
                CashbackAwarded = 4.50m,
                CreatedAt = now.AddDays(-1).AddHours(-2)
            },
            // 2. مطعم اللمسة الملكية (Approved)
            new()
            {
                InvoiceId = invRoyal,
                CustomerId = "CUST-005",
                TenantId = "TEN-ROYAL-TOUCH",
                ExtractedMerchant = "مطعم اللمسة الملكية للمأكولات",
                InvoiceNumber = "INV-RT-8840",
                InvoiceDate = now.AddDays(-2).AddHours(-5),
                TotalAmount = 280.00m,
                ConfidenceScore = 0.9940m,
                RawAiPayload = "{\"merchant\": \"مطعم اللمسة الملكية\", \"total\": 280.00, \"itemsCount\": 4}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=600&h=800&fit=crop",
                ImagePHash = "b2c3d4e5f6a70022",
                FraudRiskScore = 11,
                ProcessingStatus = InvoiceStatus.APPROVED,
                PointsAwarded = 0,
                CashbackAwarded = 14.00m,
                CreatedAt = now.AddDays(-2).AddHours(-5)
            },
            // 3. فساتين السهرة (Approved)
            new()
            {
                InvoiceId = invDress,
                CustomerId = "CUST-002",
                TenantId = "TEN-EVENING-DRESS",
                ExtractedMerchant = "بوتيك فساتين السهرة والأزياء الراقية",
                InvoiceNumber = "INV-ED-1092",
                InvoiceDate = now.AddDays(-3).AddHours(-1),
                TotalAmount = 1200.00m,
                ConfidenceScore = 0.9910m,
                RawAiPayload = "{\"merchant\": \"فساتين السهرة\", \"total\": 1200.00, \"itemsCount\": 1}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1566174053879-31528523f8ae?w=600&h=800&fit=crop",
                ImagePHash = "c3d4e5f6a7b80033",
                FraudRiskScore = 15,
                ProcessingStatus = InvoiceStatus.APPROVED,
                PointsAwarded = 180,
                CashbackAwarded = 0.00m,
                CreatedAt = now.AddDays(-3).AddHours(-1)
            },
            // 4. محل صقر للألعاب (Approved)
            new()
            {
                InvoiceId = invSaqr,
                CustomerId = "CUST-003",
                TenantId = "TEN-SAQR-GAMES",
                ExtractedMerchant = "محل صقر للألعاب والترفيه الإلكتروني",
                InvoiceNumber = "INV-SQ-3011",
                InvoiceDate = now.AddDays(-1).AddHours(-6),
                TotalAmount = 450.00m,
                ConfidenceScore = 0.9780m,
                RawAiPayload = "{\"merchant\": \"صقر للألعاب\", \"total\": 450.00, \"itemsCount\": 2}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=600&h=800&fit=crop",
                ImagePHash = "d4e5f6a7b8c90044",
                FraudRiskScore = 10,
                ProcessingStatus = InvoiceStatus.APPROVED,
                PointsAwarded = 45,
                CashbackAwarded = 0.00m,
                CreatedAt = now.AddDays(-1).AddHours(-6)
            },
            // 5. محل للشكلاطة (Approved)
            new()
            {
                InvoiceId = invChoc,
                CustomerId = "CUST-006",
                TenantId = "TEN-CHOCOLATE",
                ExtractedMerchant = "قصر الشوكولاتة والحلويات الفاخرة",
                InvoiceNumber = "INV-CH-4410",
                InvoiceDate = now.AddHours(-12),
                TotalAmount = 160.00m,
                ConfidenceScore = 0.9850m,
                RawAiPayload = "{\"merchant\": \"قصر الشكلاطة\", \"total\": 160.00, \"itemsCount\": 3}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1548907040-4baa42d10919?w=600&h=800&fit=crop",
                ImagePHash = "e5f6a7b8c9d00055",
                FraudRiskScore = 9,
                ProcessingStatus = InvoiceStatus.APPROVED,
                PointsAwarded = 0,
                CashbackAwarded = 12.80m,
                CreatedAt = now.AddHours(-12)
            },
            // 6. مواد التنظيف (Approved)
            new()
            {
                InvoiceId = invCleaning,
                CustomerId = "CUST-007",
                TenantId = "TEN-CLEANING",
                ExtractedMerchant = "مواد التنظيف الفاخرة والعناية المنزلية",
                InvoiceNumber = "INV-CL-9901",
                InvoiceDate = now.AddDays(-2).AddHours(-4),
                TotalAmount = 185.00m,
                ConfidenceScore = 0.9820m,
                RawAiPayload = "{\"merchant\": \"مواد التنظيف\", \"total\": 185.00, \"itemsCount\": 4}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1585421514738-01798e348b17?w=600&h=800&fit=crop",
                ImagePHash = "f6a7b8c9d0e10066",
                FraudRiskScore = 12,
                ProcessingStatus = InvoiceStatus.APPROVED,
                PointsAwarded = 18,
                CashbackAwarded = 0.00m,
                CreatedAt = now.AddDays(-2).AddHours(-4)
            },
            // 7. فاتورة قيد المراجعة للتدقيق (Assigned to مراد)
            new()
            {
                InvoiceId = invSuspect1,
                CustomerId = "CUST-001",
                TenantId = "TEN-EVENING-DRESS",
                ExtractedMerchant = "بوتيك فساتين السهرة والأزياء الراقية",
                InvoiceNumber = "INV-ED-9931",
                InvoiceDate = now.AddHours(-3),
                TotalAmount = 2450.00m,
                ConfidenceScore = 0.9350m,
                RawAiPayload = "{\"merchant\": \"فساتين السهرة\", \"total\": 2450.00, \"itemsCount\": 2}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1566174053879-31528523f8ae?w=600&h=800&fit=crop",
                ImagePHash = "c3d4e5f6a7b80033", // Match pHash with invDress
                FraudRiskScore = 65,
                ProcessingStatus = InvoiceStatus.REVIEW_REQUIRED,
                PointsAwarded = 245,
                CreatedAt = now.AddHours(-3)
            },
            // 8. فاتورة مشبوهة ثانية (Assigned to مراد)
            new()
            {
                InvoiceId = invSuspect2,
                CustomerId = "CUST-003",
                TenantId = "TEN-SAQR-GAMES",
                ExtractedMerchant = "محل صقر للألعاب والترفيه الإلكتروني",
                InvoiceNumber = "INV-SQ-9988",
                InvoiceDate = now.AddHours(-7),
                TotalAmount = 3850.00m,
                ConfidenceScore = 0.9520m,
                RawAiPayload = "{\"merchant\": \"صقر للألعاب\", \"total\": 3850.00, \"itemsCount\": 1}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=600&h=800&fit=crop",
                ImagePHash = "a7b8c9d0e1f20077",
                FraudRiskScore = 55,
                ProcessingStatus = InvoiceStatus.REVIEW_REQUIRED,
                PointsAwarded = 385,
                CreatedAt = now.AddHours(-7)
            },
            // 9. فاتورة مرفوضة لاحتيال عالي
            new()
            {
                InvoiceId = invRejected1,
                CustomerId = "CUST-004",
                TenantId = "TEN-GROCERY",
                ExtractedMerchant = "سوبرماركت المواد الغذائية والتموينية",
                InvoiceNumber = "INV-GR-0099",
                InvoiceDate = now.AddDays(-2).AddHours(-9),
                TotalAmount = 1950.00m,
                ConfidenceScore = 0.6200m,
                RawAiPayload = "{\"merchant\": \"المواد الغذائية\", \"total\": 1950.00, \"itemsCount\": 2}",
                ReceiptImageUrl = "https://images.unsplash.com/photo-1578916171728-46686eac8d58?w=600&h=800&fit=crop",
                ImagePHash = "b8c9d0e1f2a30088",
                FraudRiskScore = 95,
                ProcessingStatus = InvoiceStatus.REJECTED,
                RejectionReason = "تكرار رفع فاتورة مستبدلة وتعديل الأرقام فوتوشوبياً",
                PointsAwarded = 0,
                CreatedAt = now.AddDays(-2).AddHours(-9)
            }
        };

        foreach (var inv in seedInvoices)
        {
            if (!await context.Invoices.AnyAsync(x => x.TenantId == inv.TenantId && x.InvoiceNumber == inv.InvoiceNumber))
            {
                await context.Invoices.AddAsync(inv);
            }
        }
        await context.SaveChangesAsync();

        // ----------------------------------------------------
        // 8. 📦 TABLE: InvoiceLineItems (أصناف الفواتير)
        // ----------------------------------------------------
        if (!await context.InvoiceLineItems.AnyAsync())
        {
            var lineItems = new List<InvoiceLineItem>
            {
                // كافي كادي
                new() { InvoiceId = invKadi, ItemName = "قهوة فلات وايت مختصة (Flat White)", Quantity = 2, UnitPrice = 15.00m, TotalPrice = 30.00m, Confidence = 0.99m },
                new() { InvoiceId = invKadi, ItemName = "كرواسون شوكولاتة وكراميل", Quantity = 1, UnitPrice = 15.00m, TotalPrice = 15.00m, Confidence = 0.98m },

                // مطعم اللمسة الملكية
                new() { InvoiceId = invRoyal, ItemName = "مشاوي مشكلة فاخرة رويال", Quantity = 2, UnitPrice = 90.00m, TotalPrice = 180.00m, Confidence = 0.99m },
                new() { InvoiceId = invRoyal, ItemName = "سلطة سيزر ملكية بالدجاج", Quantity = 1, UnitPrice = 40.00m, TotalPrice = 40.00m, Confidence = 0.98m },
                new() { InvoiceId = invRoyal, ItemName = "مقبلات رويال ساخنة", Quantity = 1, UnitPrice = 35.00m, TotalPrice = 35.00m, Confidence = 0.97m },
                new() { InvoiceId = invRoyal, ItemName = "عصائر طبيعية طازجة", Quantity = 2, UnitPrice = 12.50m, TotalPrice = 25.00m, Confidence = 0.99m },

                // فساتين السهرة
                new() { InvoiceId = invDress, ItemName = "فستان سهرة كلاسيك مطرز يدوي (Royal Evening Gown)", Quantity = 1, UnitPrice = 1200.00m, TotalPrice = 1200.00m, Confidence = 0.99m },

                // صقر للألعاب
                new() { InvoiceId = invSaqr, ItemName = "يد تحكم بلايستيشن 5 لاسلكية (PS5 Controller)", Quantity = 1, UnitPrice = 320.00m, TotalPrice = 320.00m, Confidence = 0.98m },
                new() { InvoiceId = invSaqr, ItemName = "بطاقة شحن رصيد ألعاب PlayStation 25$", Quantity = 1, UnitPrice = 130.00m, TotalPrice = 130.00m, Confidence = 0.97m },

                // قصر الشكلاطة
                new() { InvoiceId = invChoc, ItemName = "علبة شوكولاتة بلجيكية فاخرة مشكلة 500 جرام", Quantity = 1, UnitPrice = 95.00m, TotalPrice = 95.00m, Confidence = 0.99m },
                new() { InvoiceId = invChoc, ItemName = "ترافل شوكولاتة داكنة بالبندق", Quantity = 1, UnitPrice = 40.00m, TotalPrice = 40.00m, Confidence = 0.98m },
                new() { InvoiceId = invChoc, ItemName = "لوح شوكولاتة سويسرية بالفستق", Quantity = 1, UnitPrice = 25.00m, TotalPrice = 25.00m, Confidence = 0.97m },

                // مواد التنظيف
                new() { InvoiceId = invCleaning, ItemName = "سائل غسيل ملابس مركز 3 لتر", Quantity = 2, UnitPrice = 45.00m, TotalPrice = 90.00m, Confidence = 0.98m },
                new() { InvoiceId = invCleaning, ItemName = "مجموعة معطرات ومطهرات أرضيات فاخرة", Quantity = 1, UnitPrice = 55.00m, TotalPrice = 55.00m, Confidence = 0.97m },
                new() { InvoiceId = invCleaning, ItemName = "مناديل مبللة ومنظف زجاج كريستال", Quantity = 2, UnitPrice = 20.00m, TotalPrice = 40.00m, Confidence = 0.96m }
            };

            await context.InvoiceLineItems.AddRangeAsync(lineItems);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 9. 🛡️ TABLE: FraudReviewLogs (سجلات التدقيق والاحتيال)
        // ----------------------------------------------------
        if (!await context.FraudReviewLogs.AnyAsync())
        {
            var fraudLogs = new List<FraudReviewLog>
            {
                new()
                {
                    InvoiceId = invSuspect1,
                    AssignedAuditorId = "USR-MOURAD", // Assigned to مراد
                    CalculatedRiskScore = 65,
                    TriggeredFlags = "[\"PHASH_DUPLICATE_MATCH_95%\", \"HIGH_VALUE_THRESHOLD\"]",
                    AuditorDecision = null, // Pending review
                    DecisionReason = null,
                    ResolvedAt = null,
                    CreatedAt = now.AddHours(-3)
                },
                new()
                {
                    InvoiceId = invSuspect2,
                    AssignedAuditorId = "USR-MOURAD", // Assigned to مراد
                    CalculatedRiskScore = 55,
                    TriggeredFlags = "[\"HIGH_VALUE_TRANSACTION\", \"DEVICE_RAPID_VELOCITY\"]",
                    AuditorDecision = null,
                    DecisionReason = null,
                    ResolvedAt = null,
                    CreatedAt = now.AddHours(-7)
                },
                new()
                {
                    InvoiceId = invRejected1,
                    AssignedAuditorId = "USR-MOURAD",
                    CalculatedRiskScore = 95,
                    TriggeredFlags = "[\"PHOTOSHOP_MANIPULATION\", \"TOTAL_TAMPERING\"]",
                    AuditorDecision = "REJECTED",
                    DecisionReason = "تم التلاعب الرقمي بالفاتورة وتعديل القيم الحسابية",
                    ResolvedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-2).AddHours(-9)
                }
            };

            await context.FraudReviewLogs.AddRangeAsync(fraudLogs);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 10. ⚖️ TABLE: TenantSettlements (التسويات المالية للمحلات)
        // ----------------------------------------------------
        if (!await context.TenantSettlements.AnyAsync())
        {
            var settlements = new List<TenantSettlement>
            {
                new()
                {
                    SettlementId = Guid.NewGuid(),
                    TenantId = "TEN-KADI-CAFE",
                    PeriodStartDate = new DateTime(2026, 9, 1),
                    PeriodEndDate = new DateTime(2026, 9, 30),
                    TotalVouchersRedeemedValue = 1850.00m,
                    MallServiceFee = 92.50m,
                    NetPayableToTenant = 1757.50m,
                    SettlementStatus = SettlementStatus.SETTLED,
                    SettledByUserId = "USR-MOSTAFA",
                    SettledAt = now.AddDays(-2)
                },
                new()
                {
                    SettlementId = Guid.NewGuid(),
                    TenantId = "TEN-ROYAL-TOUCH",
                    PeriodStartDate = new DateTime(2026, 9, 1),
                    PeriodEndDate = new DateTime(2026, 9, 30),
                    TotalVouchersRedeemedValue = 2400.00m,
                    MallServiceFee = 120.00m,
                    NetPayableToTenant = 2280.00m,
                    SettlementStatus = SettlementStatus.SETTLED,
                    SettledByUserId = "USR-MOSTAFA",
                    SettledAt = now.AddDays(-1)
                },
                new()
                {
                    SettlementId = Guid.NewGuid(),
                    TenantId = "TEN-SAQR-GAMES",
                    PeriodStartDate = new DateTime(2026, 10, 1),
                    PeriodEndDate = new DateTime(2026, 10, 15),
                    TotalVouchersRedeemedValue = 980.00m,
                    MallServiceFee = 49.00m,
                    NetPayableToTenant = 931.00m,
                    SettlementStatus = SettlementStatus.PENDING_APPROVAL,
                    SettledByUserId = null,
                    SettledAt = null
                },
                new()
                {
                    SettlementId = Guid.NewGuid(),
                    TenantId = "TEN-EVENING-DRESS",
                    PeriodStartDate = new DateTime(2026, 10, 1),
                    PeriodEndDate = new DateTime(2026, 10, 15),
                    TotalVouchersRedeemedValue = 3200.00m,
                    MallServiceFee = 160.00m,
                    NetPayableToTenant = 3040.00m,
                    SettlementStatus = SettlementStatus.PENDING_APPROVAL,
                    SettledByUserId = null,
                    SettledAt = null
                }
            };

            await context.TenantSettlements.AddRangeAsync(settlements);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 11. 🎁 TABLE: Rewards & Redemptions
        // ----------------------------------------------------
        if (!await context.Rewards.AnyAsync())
        {
            var reward1 = Guid.NewGuid();
            var reward2 = Guid.NewGuid();
            var reward3 = Guid.NewGuid();

            var seedRewards = new List<Reward>
            {
                new()
                {
                    Id = reward1,
                    Title = "فنجان قهوة مختصة مجاناً في كافي كادي",
                    Description = "استبدل نقاطك بكوب قهوة مختصة أو لاتيه ساخن أو مثلج من كافي كادي.",
                    MerchantName = "كافي كادي (Kadi Café)",
                    PointsCost = 40,
                    DiscountValue = 15.00m,
                    StockQuantity = 100,
                    IsActive = true,
                    ValidityDays = 30,
                    CreatedAt = now.AddMonths(-3)
                },
                new()
                {
                    Id = reward2,
                    Title = "قسيمة خصم 50 دينار في بوتيك فساتين السهرة",
                    Description = "خصم 50 د.ل عند شراء أي فستان سهرة أو مناسبات بقيمة 300 د.ل أو أكثر.",
                    MerchantName = "بوتيك فساتين السهرة",
                    PointsCost = 150,
                    DiscountValue = 50.00m,
                    StockQuantity = 30,
                    IsActive = true,
                    ValidityDays = 45,
                    CreatedAt = now.AddMonths(-3)
                },
                new()
                {
                    Id = reward3,
                    Title = "خصم 25 دينار في مطعم اللمسة الملكية",
                    Description = "استمتع بوجبة ملكية فاخرة بخصم 25 د.ل على الفاتورة الإجمالية.",
                    MerchantName = "مطعم اللمسة الملكية",
                    PointsCost = 80,
                    DiscountValue = 25.00m,
                    StockQuantity = 50,
                    IsActive = true,
                    ValidityDays = 30,
                    CreatedAt = now.AddMonths(-2)
                }
            };

            await context.Rewards.AddRangeAsync(seedRewards);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 12. 📢 TABLE: Promotion Banners
        // ----------------------------------------------------
        if (!await context.PromotionBanners.AnyAsync())
        {
            var banners = new List<PromotionBanner>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "مهرجان التذوق في كافي كادي ومطعم اللمسة الملكية",
                    Subtitle = "استمتع بأشهى المأكولات والمشروبات واكسب كاش باك ونقاط مضاعفة",
                    ImageUrl = "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=800",
                    TargetUrl = "/AdminDashboard/Stores",
                    TargetAudience = "ALL",
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAt = now.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "تشكيلة فساتين السهرة الفاخرة لعام 2026",
                    Subtitle = "أحدث صيحات الموضة والأزياء الراقية لدى بوتيك فساتين السهرة بالمول",
                    ImageUrl = "https://images.unsplash.com/photo-1566174053879-31528523f8ae?w=800",
                    TargetUrl = "/AdminDashboard/Loyalty",
                    TargetAudience = "ALL",
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAt = now.AddDays(-4)
                }
            };

            await context.PromotionBanners.AddRangeAsync(banners);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 13. 🏆 TABLE: LoyaltyCampaigns
        // ----------------------------------------------------
        if (!await context.LoyaltyCampaigns.AnyAsync())
        {
            var campaigns = new List<LoyaltyCampaign>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "حملة الجمعة الذهبية (Double Points Friday)",
                    Description = "مضاعفة نقاط الولاء 2x على كافة الفواتير الممسوحة أيام الجمعة",
                    PointsMultiplier = 2.00m,
                    MinSpendAmount = 50.00m,
                    StartDate = now.AddDays(-14),
                    EndDate = now.AddDays(30),
                    IsActive = true,
                    CreatedAt = now.AddDays(-14)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "مهرجان قهوة الصباح في كافي كادي",
                    Description = "كاش باك 15% على جميع طلبات القهوة الصباحية حتى الساعة 12 ظهراً",
                    PointsMultiplier = 1.50m,
                    MinSpendAmount = 15.00m,
                    StartDate = now.AddDays(-7),
                    EndDate = now.AddDays(60),
                    IsActive = true,
                    CreatedAt = now.AddDays(-7)
                }
            };

            await context.LoyaltyCampaigns.AddRangeAsync(campaigns);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 14. ⚡ TABLE: ProductBoosterRules
        // ----------------------------------------------------
        if (!await context.ProductBoosterRules.AnyAsync())
        {
            var boosters = new List<ProductBoosterRule>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "بونص شوكولاتة كندر وترافل",
                    BrandOrItemKeyword = "Kinder",
                    TargetCategory = "Sweets & Chocolates",
                    TenantId = "TEN-CHOCOLATE",
                    BonusPoints = 25,
                    PointsMultiplier = 1.20m,
                    SponsorName = "محل الشكلاطة",
                    StartDate = now.AddDays(-10),
                    EndDate = now.AddDays(60),
                    IsActive = true,
                    CreatedAt = now.AddDays(-10)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "بونص قهوة مختصة كادي",
                    BrandOrItemKeyword = "Flat White",
                    TargetCategory = "Cafes & Coffee",
                    TenantId = "TEN-KADI-CAFE",
                    BonusPoints = 15,
                    PointsMultiplier = 1.00m,
                    SponsorName = "كافي كادي",
                    StartDate = now.AddDays(-10),
                    EndDate = now.AddDays(60),
                    IsActive = true,
                    CreatedAt = now.AddDays(-10)
                }
            };

            await context.ProductBoosterRules.AddRangeAsync(boosters);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 15. 📐 TABLE: LoyaltyEarningRules
        // ----------------------------------------------------
        if (!await context.LoyaltyEarningRules.AnyAsync())
        {
            var earningRules = new List<LoyaltyEarningRule>
            {
                new()
                {
                    StoreTenantId = null,
                    RuleName = "القاعدة العامة الموحدة للمول (Spend Based)",
                    CalculationType = "SPEND_RATIO",
                    SpendRatioRate = 0.1000m,
                    MinInvoiceAmount = 20.00m,
                    MaxPointsCap = 500,
                    IsActive = true,
                    CreatedAt = now.AddMonths(-6)
                },
                new()
                {
                    StoreTenantId = "TEN-KADI-CAFE",
                    RuleName = "نقاط زيارة كادي الثابتة (Flat Visit Reward)",
                    CalculationType = "FIXED_PER_INVOICE",
                    FixedPointsPerInvoice = 5,
                    MinInvoiceAmount = 10.00m,
                    MaxPointsCap = 50,
                    IsActive = true,
                    CreatedAt = now.AddMonths(-5)
                }
            };

            await context.LoyaltyEarningRules.AddRangeAsync(earningRules);
            await context.SaveChangesAsync();
        }

        // ----------------------------------------------------
        // 13. 🛡️ TABLE: Audit Logs
        // ----------------------------------------------------
        if (!await context.AuditLogs.AnyAsync())
        {
            var logs = new List<AuditLog>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PerformedBy = "مصطفى صوان (المدير العام)",
                    Action = "SYSTEM_INITIALIZATION",
                    EntityName = "Database",
                    EntityId = "MMAppDb",
                    Details = "تهيئة بيانات النظام المركزية والمتاجر وقواعد الولاء وربط ترويسات الـ OCR",
                    IpAddress = "127.0.0.1",
                    CreatedAt = now.AddDays(-10)
                },
                new()
                {
                    PerformedBy = "مراد المنفي (مدقق الفواتير)",
                    Action = "FRAUD_AUDIT_ACTION",
                    EntityName = "Invoice",
                    EntityId = "INV-GR-0099",
                    Details = "رفض فاتورة سوبرماركت معدلة فوتوشوبياً وحظر محاولة التحايل المالي",
                    IpAddress = "192.168.1.55",
                    CreatedAt = now.AddDays(-2)
                }
            };

            await context.AuditLogs.AddRangeAsync(logs);
            await context.SaveChangesAsync();
        }
    }
}
