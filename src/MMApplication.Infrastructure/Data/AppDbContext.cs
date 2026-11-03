using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Core Tables
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantLoyaltyRule> TenantLoyaltyRules => Set<TenantLoyaltyRule>();
    public DbSet<SystemUser> SystemUsers => Set<SystemUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<CustomerWallet> CustomerWallets => Set<CustomerWallet>();
    public DbSet<WalletLedger> WalletLedger => Set<WalletLedger>();
    public DbSet<TenantSettlement> TenantSettlements => Set<TenantSettlement>();
    public DbSet<FraudReviewLog> FraudReviewLogs => Set<FraudReviewLog>();

    // Advanced Mall Engine Tables
    public DbSet<StoreAlias> StoreAliases => Set<StoreAlias>();
    public DbSet<CustomerDevice> CustomerDevices => Set<CustomerDevice>();
    public DbSet<ProductBoosterRule> ProductBoosterRules => Set<ProductBoosterRule>();
    public DbSet<LoyaltyEarningRule> LoyaltyEarningRules => Set<LoyaltyEarningRule>();

    // Auxiliary Mall Tables
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<RewardRedemption> Redemptions => Set<RewardRedemption>();
    public DbSet<PromotionBanner> PromotionBanners => Set<PromotionBanner>();
    public DbSet<LoyaltyCampaign> LoyaltyCampaigns => Set<LoyaltyCampaign>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Legacy aliases for backward compatibility if needed
    public DbSet<Tenant> StoreTenants => Set<Tenant>();
    public DbSet<TenantSettlement> StoreSettlements => Set<TenantSettlement>();
    public DbSet<WalletLedger> PointTransactions => Set<WalletLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
