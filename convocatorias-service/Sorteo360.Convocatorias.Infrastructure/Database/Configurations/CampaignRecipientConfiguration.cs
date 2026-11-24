using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class CampaignRecipientConfiguration : IEntityTypeConfiguration<CampaignRecipientData>
{
    public void Configure(EntityTypeBuilder<CampaignRecipientData> builder)
    {
        builder.ToTable("campaign_recipient", ConvocatoriasDbContext.SchemaName, t =>
        {
            t.HasCheckConstraint(
                "ck_campaign_recipient_send_status",
                $"send_status IN ({Quoted(RecipientSendStatus.All)})");

            // Una baja sincronizada sin baja no se puede leer: el sello de sincronización dice
            // "el CRM ya sabe de este clic", y sin el clic no hay nada que el CRM sepa.
            t.HasCheckConstraint(
                "ck_campaign_recipient_sync_requires_unsubscribe",
                "unsubscribe_synced_at IS NULL OR unsubscribed_at IS NOT NULL");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.CampaignId)
            .HasColumnName("campaign_id")
            .IsRequired();

        builder.Property(r => r.ContactId)
            .HasColumnName("contact_id")
            .IsRequired();

        builder.Property(r => r.FirstName)
            .HasColumnName("first_name")
            .IsRequired();

        builder.Property(r => r.LastName)
            .HasColumnName("last_name")
            .IsRequired();

        builder.Property(r => r.Email)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(r => r.SendStatus)
            .HasColumnName("send_status")
            .IsRequired();

        builder.Property(r => r.FailureReason)
            .HasColumnName("failure_reason");

        builder.Property(r => r.NotificationId)
            .HasColumnName("notification_id");

        builder.Property(r => r.RegistrationTokenHash)
            .HasColumnName("registration_token_hash");

        builder.Property(r => r.UnsubscribeTokenHash)
            .HasColumnName("unsubscribe_token_hash");

        builder.Property(r => r.UnsubscribedAt)
            .HasColumnName("unsubscribed_at");

        builder.Property(r => r.UnsubscribeSyncedAt)
            .HasColumnName("unsubscribe_synced_at");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // El grano, en la base y no solo en el código: una persona está una vez en una campaña.
        builder.HasIndex(r => new { r.CampaignId, r.ContactId }, "ux_campaign_recipient_campaign_contact")
            .IsUnique();

        // El cursor de reanudación: el worker y el `resume-handoff` de la spec 04 §5 piden las
        // filas pendientes de una campaña, y es la consulta que corre por cada lote.
        builder.HasIndex(r => new { r.CampaignId, r.SendStatus }, "ix_campaign_recipient_campaign_send_status");

        // La búsqueda por token del portal (spec 06). El índice va desde ya porque la columna
        // ya está: añadirlo después obliga a una migración sobre una tabla con filas.
        builder.HasIndex(r => r.RegistrationTokenHash, "ix_campaign_recipient_registration_token_hash");
        builder.HasIndex(r => r.UnsubscribeTokenHash, "ix_campaign_recipient_unsubscribe_token_hash");
    }

    /// <summary>Los valores de un conjunto cerrado, entrecomillados para un <c>IN</c> de SQL.</summary>
    private static string Quoted(IReadOnlySet<string> values) =>
        string.Join(", ", values.Order(StringComparer.Ordinal).Select(value => $"'{value}'"));
}
