using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class PlanRequestConfiguration : IEntityTypeConfiguration<PlanRequestData>
{
    public void Configure(EntityTypeBuilder<PlanRequestData> builder)
    {
        builder.ToTable("plan_request", "events", t =>
        {
            t.HasCheckConstraint("ck_plan_request_plan", "requested_plan IN ('basic', 'pro', 'advanced')");
            t.HasCheckConstraint("ck_plan_request_status", "status IN ('Pending', 'Approved', 'Rejected', 'Cancelled')");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(r => r.RequestedPlan).HasColumnName("requested_plan").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
        builder.Property(r => r.TransferReference).HasColumnName("transfer_reference").HasMaxLength(200);
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(r => r.RequestedById).HasColumnName("requested_by_id").IsRequired();
        builder.Property(r => r.RequesterEmail).HasColumnName("requester_email").HasMaxLength(320);
        builder.Property(r => r.DecidedAt).HasColumnName("decided_at");
        builder.Property(r => r.DecidedById).HasColumnName("decided_by_id");
        builder.Property(r => r.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.Property(r => r.PaidPeriodStart).HasColumnName("paid_period_start");
        builder.Property(r => r.PaidPeriodEnd).HasColumnName("paid_period_end");

        builder.HasOne<AccountData>()
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .HasConstraintName("fk_plan_request_account")
            .OnDelete(DeleteBehavior.Cascade);

        // Solo una solicitud pendiente por cuenta a la vez (§9.5).
        builder.HasIndex(r => r.AccountId)
            .HasDatabaseName("ux_plan_request_account_pending")
            .IsUnique()
            .HasFilter("status = 'Pending'");

        builder.HasIndex(r => new { r.Status, r.RequestedAt })
            .HasDatabaseName("ix_plan_request_status_requested_at");
    }
}

