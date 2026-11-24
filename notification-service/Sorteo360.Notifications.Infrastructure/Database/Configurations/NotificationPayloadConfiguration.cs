using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Notifications.Infrastructure.Database.Configurations;

internal sealed class NotificationPayloadConfiguration : IEntityTypeConfiguration<NotificationPayloadData>
{
    public void Configure(EntityTypeBuilder<NotificationPayloadData> builder)
    {
        builder.ToTable("notification_payload", NotificationsDbContext.SchemaName);

        // La clave es la propia fila de persona: exactamente un juego de valores por envío
        // individual, y nada que limpiar si alguien insertara dos veces.
        builder.HasKey(p => p.RecipientId);

        builder.Property(p => p.RecipientId)
            .HasColumnName("recipient_id");

        // "values" es palabra reservada de SQL: hoy pasa solo porque toda referencia va calificada
        // (p.values), y una consulta escrita con el nombre pelado dejaría de compilar en la base.
        builder.Property(p => p.RenderValues)
            .HasColumnName("render_values")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // En cascada y no Restrict, al revés que el resto: estas filas son efímeras por diseño, así
        // que borrar el envío que las explica tiene que poder llevárselas.
        builder.HasOne<NotificationRecipientData>()
            .WithMany()
            .HasForeignKey(p => p.RecipientId)
            .HasConstraintName("fk_notification_payload_recipient")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
