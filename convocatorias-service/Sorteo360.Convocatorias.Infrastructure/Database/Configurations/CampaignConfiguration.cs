using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<CampaignData>
{
    public void Configure(EntityTypeBuilder<CampaignData> builder)
    {
        builder.ToTable("campaign", ConvocatoriasDbContext.SchemaName, t =>
        {
            // Conjuntos cerrados, los mismos que guardan las entidades de dominio. Acá para que
            // ninguna escritura futura que no pase por el repositorio los pueda saltar.
            t.HasCheckConstraint(
                "ck_campaign_state",
                $"state IN ({Quoted(CampaignState.All)})");

            t.HasCheckConstraint(
                "ck_campaign_language",
                $"language IN ({Quoted(AllowedLanguage.All)})");

            // Quién lanzó y cuándo van juntos o no van: media estampa de lanzamiento no
            // significa nada, y una fila con una sola de las dos columnas no se puede leer.
            t.HasCheckConstraint(
                "ck_campaign_launch_stamp_is_whole",
                "(launched_by_id IS NULL) = (launched_at IS NULL)");

            // Una campaña sin lanzar no puede estar enviada: sent_at lo estampa el worker
            // después del lanzamiento, así que no hay forma legítima de tener lo segundo sin lo
            // primero.
            t.HasCheckConstraint(
                "ck_campaign_sent_requires_launch",
                "sent_at IS NULL OR launched_at IS NOT NULL");

            // Los tres valores del evento van juntos o no van. Uno solo no rinde ningún correo, y
            // media copia se leería como una campaña rendible que luego falla en el worker.
            // No se ata a launched_at a propósito: las campañas lanzadas antes de que existieran
            // estas columnas llevan la estampa y no los valores, y una restricción que ellas
            // incumplan no deja aplicar la migración.
            t.HasCheckConstraint(
                "ck_campaign_event_values_are_whole",
                "(event_name IS NULL) = (event_location IS NULL)"
                + " AND (event_name IS NULL) = (event_dates IS NULL)");

            // Sin evento si y solo si es un mensaje directo, y entonces con su asunto y su texto
            // (ver CampaignDirectMessage). Las filas que ya existen tienen evento, plantilla de
            // evento y las dos columnas nuevas nulas, así que la cumplen al aplicar la migración.
            t.HasCheckConstraint(
                "ck_campaign_direct_message_is_whole",
                $"(event_id IS NULL) = (template_key = '{TemplateKey.DirectMessage}')"
                + " AND (event_id IS NULL) = (subject IS NOT NULL)"
                + " AND (event_id IS NULL) = (message IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.EventId)
            .HasColumnName("event_id");

        builder.Property(c => c.Subject)
            .HasColumnName("subject");

        builder.Property(c => c.Message)
            .HasColumnName("message");

        builder.Property(c => c.TemplateKey)
            .HasColumnName("template_key")
            .IsRequired();

        builder.Property(c => c.Language)
            .HasColumnName("language")
            .IsRequired();

        builder.Property(c => c.State)
            .HasColumnName("state")
            .IsRequired();

        builder.Property(c => c.LaunchedById)
            .HasColumnName("launched_by_id");

        builder.Property(c => c.LaunchedAt)
            .HasColumnName("launched_at");

        builder.Property(c => c.SentAt)
            .HasColumnName("sent_at");

        builder.Property(c => c.EventName)
            .HasColumnName("event_name");

        builder.Property(c => c.EventLocation)
            .HasColumnName("event_location");

        builder.Property(c => c.EventDates)
            .HasColumnName("event_dates");

        builder.Property(c => c.SenderEmail)
            .HasColumnName("sender_email");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(c => c.UpdatedById)
            .HasColumnName("updated_by_id")
            .IsRequired();

        // Los dos filtros del listado de la spec 03 §7, y el par que usa el resumen por evento
        // de la 07.
        builder.HasIndex(c => c.EventId, "ix_campaign_event_id");
        builder.HasIndex(c => c.State, "ix_campaign_state");

        builder.HasMany(c => c.Recipients)
            .WithOne(r => r.Campaign)
            .HasForeignKey(r => r.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Exclusions)
            .WithOne(e => e.Campaign)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Los valores de un conjunto cerrado, entrecomillados para un <c>IN</c> de SQL.</summary>
    private static string Quoted(IReadOnlySet<string> values) =>
        string.Join(", ", values.Order(StringComparer.Ordinal).Select(value => $"'{value}'"));
}
