using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Notifications.Core.Notifications;

namespace Sorteo360.Notifications.Infrastructure.Database.Configurations;

internal sealed class NotificationBatchConfiguration : IEntityTypeConfiguration<NotificationBatchData>
{
    /// <summary>Nombre del índice único de la clave de idempotencia, que el repositorio reconoce.</summary>
    public const string IdempotencyKeyIndex = "ux_notification_batch_idempotency_key";

    public void Configure(EntityTypeBuilder<NotificationBatchData> builder)
    {
        builder.ToTable("notification_batch", NotificationsDbContext.SchemaName, t =>
        {
            // Conjunto cerrado de idiomas. El servicio no usa el idioma, pero un valor inventado
            // en la columna no lo podría interpretar nadie años después.
            t.HasCheckConstraint(
                "ck_notification_batch_language",
                $"language IN ({string.Join(", ", AllowedLanguage.All.Select(language => $"'{language}'"))})");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(b => b.FromAddress)
            .HasColumnName("from_address")
            .IsRequired();

        builder.Property(b => b.FromName)
            .HasColumnName("from_name");

        builder.Property(b => b.Subject)
            .HasColumnName("subject")
            .IsRequired();

        builder.Property(b => b.Body)
            .HasColumnName("body")
            .IsRequired();

        builder.Property(b => b.Language)
            .HasColumnName("language")
            .IsRequired();

        builder.Property(b => b.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .IsRequired();

        builder.Property(b => b.BodySha256)
            .HasColumnName("body_sha256")
            .IsRequired();

        builder.Property(b => b.TrackingExpected)
            .HasColumnName("tracking_expected")
            .IsRequired();

        builder.Property(b => b.TraceId)
            .HasColumnName("trace_id");

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // El índice único es el que resuelve la carrera entre dos peticiones idénticas: no hay
        // comprobación previa que pueda hacerlo, porque entre leer y escribir cabe la otra
        // petición. Quien pierde lo atrapa y vuelve a leer.
        builder.HasIndex(b => b.IdempotencyKey, IdempotencyKeyIndex)
            .IsUnique();

        // El historial se lista por fecha descendente.
        builder.HasIndex(b => b.CreatedAt, "ix_notification_batch_created_at");

        builder.HasIndex(b => b.Type, "ix_notification_batch_type");
    }
}
