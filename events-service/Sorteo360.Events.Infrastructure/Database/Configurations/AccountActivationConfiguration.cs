using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class AccountActivationConfiguration : IEntityTypeConfiguration<AccountActivationData>
{
    public void Configure(EntityTypeBuilder<AccountActivationData> builder)
    {
        builder.ToTable("account_activation", "events", t =>
        {
            t.HasCheckConstraint("ck_account_activation_billing_period",
                "billing_period IN ('monthly', 'annual')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(a => a.CompanyName).HasColumnName("company_name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.EmailNormalized).HasColumnName("email_normalized").HasMaxLength(320).IsRequired();
        builder.Property(a => a.RequestedPlan).HasColumnName("requested_plan").HasMaxLength(20);
        builder.Property(a => a.BillingPeriod).HasColumnName("billing_period").HasMaxLength(20).HasDefaultValue("monthly").IsRequired();
        builder.Property(a => a.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(a => a.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(a => a.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.LastSentAt).HasColumnName("last_sent_at").IsRequired();
        builder.Property(a => a.ResendCountToday).HasColumnName("resend_count_today").IsRequired();
        builder.Property(a => a.ResendDay).HasColumnName("resend_day").IsRequired();

        builder.HasOne<AccountData>()
            .WithMany()
            .HasForeignKey(a => a.AccountId)
            .HasConstraintName("fk_account_activation_account")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.TokenHash)
            .HasDatabaseName("ux_account_activation_token_hash")
            .IsUnique();

        builder.HasIndex(a => a.EmailNormalized)
            .HasDatabaseName("ix_account_activation_email");
    }
}

