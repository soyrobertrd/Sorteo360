using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Notifications.Core.Notifications;

namespace Sorteo360.Notifications.Infrastructure.Database.Configurations;

internal sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipientData>
{
    public void Configure(EntityTypeBuilder<NotificationRecipientData> builder)
    {
        builder.ToTable("notification_recipient", NotificationsDbContext.SchemaName, t =>
        {
            t.HasCheckConstraint(
                "ck_notification_recipient_delivery_status",
                $"delivery_status IN ({Quoted(DeliveryStatus.All)})");

            t.HasCheckConstraint(
                "ck_notification_recipient_action",
                $"recipient_action IN ({Quoted(RecipientAction.All)})");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(r => r.Email)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(NotificationRecipient.NameMaxLength);

        builder.Property(r => r.DeliveryStatus)
            .HasColumnName("delivery_status")
            .IsRequired();

        builder.Property(r => r.RecipientAction)
            .HasColumnName("recipient_action")
            .IsRequired();

        builder.Property(r => r.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();

        builder.Property(r => r.NextAttemptAt)
            .HasColumnName("next_attempt_at");

        builder.Property(r => r.QueuedAt)
            .HasColumnName("queued_at")
            .IsRequired();

        builder.Property(r => r.SentAt)
            .HasColumnName("sent_at");

        builder.Property(r => r.LastError)
            .HasColumnName("last_error");

        builder.Property(r => r.SgMessageId)
            .HasColumnName("sg_message_id");

        builder.Property(r => r.SmtpQueuedAs)
            .HasColumnName("smtp_queued_as");

        builder.HasOne<NotificationBatchData>()
            .WithMany()
            .HasForeignKey(r => r.BatchId)
            .HasConstraintName("fk_notification_recipient_batch")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.BatchId, "ix_notification_recipient_batch_id");

        // El historial se filtra por dirección exacta, no por parecido.
        builder.HasIndex(r => r.Email, "ix_notification_recipient_email");

        // Lo que el worker pregunta cada vuelta: qué filas no son terminales y les toca ya.
        builder.HasIndex(r => new { r.DeliveryStatus, r.NextAttemptAt }, "ix_notification_recipient_pending");
    }

    /// <summary>Entrecomilla un conjunto de códigos para un CHECK de PostgreSQL.</summary>
    private static string Quoted(IReadOnlySet<string> values) =>
        string.Join(", ", values.Order(StringComparer.Ordinal).Select(value => $"'{value}'"));
}
