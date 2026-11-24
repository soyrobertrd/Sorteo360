using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class CampaignUploadBatchConfiguration : IEntityTypeConfiguration<CampaignUploadBatchData>
{
    public void Configure(EntityTypeBuilder<CampaignUploadBatchData> builder)
    {
        builder.ToTable("campaign_upload_batch", ConvocatoriasDbContext.SchemaName, t =>
        {
            // Quién confirmó y cuándo van juntos o no van. La mitad de una confirmación no se
            // puede leer, y es justo el dato que sostiene el aplazamiento de `onBehalfOfUserId`.
            t.HasCheckConstraint(
                "ck_campaign_upload_batch_confirmation_is_whole",
                "(confirmed_by_id IS NULL) = (confirmed_at IS NULL)");

            // Las siete cuentas suman las filas del archivo. La ecuación de §4 en la base, no
            // solo en el código: un resumen que no cuadra es un resumen que nadie puede creerse.
            t.HasCheckConstraint(
                "ck_campaign_upload_batch_counts_add_up",
                """
                matched_count + unmatched_count + suppressed_count + already_recipient_count
                + duplicate_in_file_count + malformed_count + collapsed_duplicate_count = row_count
                """);
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(b => b.CampaignId)
            .HasColumnName("campaign_id")
            .IsRequired();

        builder.Property(b => b.OriginalFileName)
            .HasColumnName("original_file_name")
            .IsRequired();

        builder.Property(b => b.DetectedEncoding)
            .HasColumnName("detected_encoding")
            .IsRequired();

        builder.Property(b => b.RowCount).HasColumnName("row_count").IsRequired();
        builder.Property(b => b.MatchedCount).HasColumnName("matched_count").IsRequired();
        builder.Property(b => b.UnmatchedCount).HasColumnName("unmatched_count").IsRequired();
        builder.Property(b => b.SuppressedCount).HasColumnName("suppressed_count").IsRequired();
        builder.Property(b => b.AlreadyRecipientCount).HasColumnName("already_recipient_count").IsRequired();
        builder.Property(b => b.DuplicateInFileCount).HasColumnName("duplicate_in_file_count").IsRequired();
        builder.Property(b => b.MalformedCount).HasColumnName("malformed_count").IsRequired();
        builder.Property(b => b.CollapsedDuplicateCount).HasColumnName("collapsed_duplicate_count").IsRequired();

        builder.Property(b => b.UploadedAt).HasColumnName("uploaded_at").IsRequired();
        builder.Property(b => b.UploadedById).HasColumnName("uploaded_by_id").IsRequired();
        builder.Property(b => b.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(b => b.ConfirmedById).HasColumnName("confirmed_by_id");

        // Una sola carga sin confirmar por campaña, y cuantas confirmadas haga falta. Subir una
        // segunda vez reemplaza a la primera, porque el motivo real de una segunda subida es que
        // el primer archivo estaba mal; dos listas de verdad se componen confirmando la primera,
        // que es lo que libera este sitio. Mismo modismo de índice parcial que usa el CRM para
        // dejar libre el correo de un contacto borrado.
        builder.HasIndex(b => b.CampaignId, "ux_campaign_upload_batch_pending_per_campaign")
            .IsUnique()
            .HasFilter("confirmed_at IS NULL");

        builder.HasOne(b => b.Campaign)
            .WithMany()
            .HasForeignKey(b => b.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
