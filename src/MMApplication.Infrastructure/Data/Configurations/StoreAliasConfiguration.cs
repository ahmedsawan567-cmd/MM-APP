using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMApplication.Domain.Entities;

namespace MMApplication.Infrastructure.Data.Configurations;

public class StoreAliasConfiguration : IEntityTypeConfiguration<StoreAlias>
{
    public void Configure(EntityTypeBuilder<StoreAlias> builder)
    {
        builder.ToTable("StoreAliases");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();

        builder.Property(a => a.TenantId).IsRequired().HasMaxLength(50);
        builder.Property(a => a.AliasName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.MatchType).HasMaxLength(30).HasDefaultValue("CONTAINS");
        builder.Property(a => a.IsActive).HasDefaultValue(true);

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.Aliases)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AliasName);
        builder.HasIndex(a => a.TenantId);
    }
}
