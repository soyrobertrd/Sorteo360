using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class AccountMonthlyUsageConfiguration : IEntityTypeConfiguration<AccountMonthlyUsageData>
{
    public void Configure(EntityTypeBuilder<AccountMonthlyUsageData> builder)
    {
        builder.ToTable("account_monthly_usage", "events");

        builder.HasKey(u => new { u.AccountId, u.YearMonth });

        builder.Property(u => u.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(u => u.YearMonth).HasColumnName("year_month").HasMaxLength(7).IsRequired();
        builder.Property(u => u.RafflesCreated).HasColumnName("raffles_created").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<AccountData>()
            .WithMany()
            .HasForeignKey(u => u.AccountId)
            .HasConstraintName("fk_account_monthly_usage_account")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

