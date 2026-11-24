using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class StubNotificationConfiguration : IEntityTypeConfiguration<StubNotificationData>
{
    public void Configure(EntityTypeBuilder<StubNotificationData> builder)
    {
        builder.ToTable("stub_notification", ConvocatoriasDbContext.SchemaName, t =>
        {
            // Los dos desenlaces del contrato y ninguno más. Que se cuele acá un `handed_off`
            // —que es vocabulario nuestro, no suyo— querría decir que alguien confundió los dos
            // lados de la costura.
            t.HasCheckConstraint(
                "ck_stub_notification_status",
                $"status IN ({Quoted(CampaignNotification.AllStatuses)})");

            t.HasCheckConstraint(
                "ck_stub_notification_language",
                $"language IN ({Quoted(AllowedLanguage.All)})");

            // Un motivo solo tiene sentido en un rechazo, y un rechazo sin motivo no se puede
            // leer. Van juntos o no van.
            t.HasCheckConstraint(
                "ck_stub_notification_reason_matches_status",
                $"(status = '{CampaignNotification.StatusRejected}') = (failure_reason IS NOT NULL)");
        });

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(n => n.BatchId).HasColumnName("batch_id").IsRequired();
        builder.Property(n => n.RecipientEmail).HasColumnName("recipient_email").IsRequired();
        builder.Property(n => n.Type).HasColumnName("type").IsRequired();
        builder.Property(n => n.Language).HasColumnName("language").IsRequired();
        builder.Property(n => n.Subject).HasColumnName("subject").IsRequired();
        builder.Property(n => n.BodyHtml).HasColumnName("body_html").IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").IsRequired();
        builder.Property(n => n.FailureReason).HasColumnName("failure_reason");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();

        // Lo único que se consulta: el lote entero, que es lo que una persona mira para ver qué
        // salió. Nada indexa el destinatario: buscar "qué le llegó a Juan" es del historial de
        // verdad, no de una tabla que se borra.
        builder.HasIndex(n => n.BatchId, "ix_stub_notification_batch");

        // Y ninguna relación, a propósito: ver StubNotificationData.
    }

    /// <summary>Los valores de un conjunto cerrado, entrecomillados para un <c>IN</c>.</summary>
    private static string Quoted(IReadOnlySet<string> values) =>
        string.Join(", ", values.Order(StringComparer.Ordinal).Select(value => $"'{value}'"));
}
