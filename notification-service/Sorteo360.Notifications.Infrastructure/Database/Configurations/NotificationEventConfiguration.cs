using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Notifications.Infrastructure.Database.Configurations;

internal sealed class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEventData>
{
    public void Configure(EntityTypeBuilder<NotificationEventData> builder)
    {
        builder.ToTable("notification_event", NotificationsDbContext.SchemaName);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.SgEventId)
            .HasColumnName("sg_event_id");

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .IsRequired();

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        // El JSON crudo del proveedor, tal como llegó.
        builder.Property(e => e.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.RecipientId)
            .HasColumnName("recipient_id");

        builder.HasOne<NotificationRecipientData>()
            .WithMany()
            .HasForeignKey(e => e.RecipientId)
            .HasConstraintName("fk_notification_event_recipient")
            .OnDelete(DeleteBehavior.Restrict);

        // Reenviar el mismo evento no puede duplicar la fila. El índice se filtra porque un evento
        // sin identificador del proveedor no colisiona con otro por el simple hecho de no tenerlo.
        builder.HasIndex(e => e.SgEventId, "ux_notification_event_sg_event_id")
            .IsUnique()
            .HasFilter("sg_event_id IS NOT NULL");

        builder.Property(e => e.DedupeKey)
            .HasColumnName("dedupe_key");

        // El otro lado de la deduplicación, para los eventos que llegan sin identificador del
        // proveedor. Filtrado por el mismo motivo que el de arriba: un evento que sí trae
        // sg_event_id deja esta columna vacía, y dos vacíos no colisionan.
        builder.HasIndex(e => e.DedupeKey, "ux_notification_event_dedupe_key")
            .IsUnique()
            .HasFilter("dedupe_key IS NOT NULL");

        builder.HasIndex(e => e.RecipientId, "ix_notification_event_recipient_id");
    }
}
