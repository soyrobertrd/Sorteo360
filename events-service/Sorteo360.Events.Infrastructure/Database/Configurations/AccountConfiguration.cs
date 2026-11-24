using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<AccountData>
{
    public void Configure(EntityTypeBuilder<AccountData> builder)
    {
        builder.ToTable("account", "events", t =>
        {
            t.HasCheckConstraint("ck_account_plan", "plan IN ('free', 'basic', 'pro', 'advanced')");
            t.HasCheckConstraint("ck_account_status", "status IN ('PendingActivation', 'Active', 'Suspended')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Plan).HasColumnName("plan").HasMaxLength(20).HasDefaultValue("free").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasMaxLength(30).HasDefaultValue("Active").IsRequired();
        builder.Property(a => a.OwnerEmail).HasColumnName("owner_email").HasMaxLength(320);
        builder.Property(a => a.PlanStartedAt).HasColumnName("plan_started_at");
        builder.Property(a => a.PlanExpiresAt).HasColumnName("plan_expires_at");
        builder.Property(a => a.ExpiryWarningSentAt).HasColumnName("expiry_warning_sent_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(a => a.PlanExpiresAt)
            .HasDatabaseName("ix_account_plan_expires_at")
            .HasFilter("plan_expires_at IS NOT NULL");
    }
}

