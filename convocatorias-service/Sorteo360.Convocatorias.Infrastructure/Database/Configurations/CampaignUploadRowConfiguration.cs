using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Uploads;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class CampaignUploadRowConfiguration : IEntityTypeConfiguration<CampaignUploadRowData>
{
    public void Configure(EntityTypeBuilder<CampaignUploadRowData> builder)
    {
        builder.ToTable("campaign_upload_row", ConvocatoriasDbContext.SchemaName, t =>
        {
            t.HasCheckConstraint(
                "ck_campaign_upload_row_bucket",
                $"bucket IN ({Quoted(UploadRowBucket.All)})");

            // Un motivo por fila sale de dos catálogos: los de fila mal formada, que calcula este
            // servicio, y los de supresión, que calcula el CRM. Se admiten los dos y ninguno más,
            // para que una errata no entre como motivo.
            t.HasCheckConstraint(
                "ck_campaign_upload_row_reason_code",
                $"reason_code IS NULL OR reason_code IN ({Quoted(MalformedRowReason.All, ExclusionReason.All)})");

            // Una fila aplicada quedó atada a un contacto. Sin esa atadura no hay nada que
            // "aplicada" pueda significar.
            t.HasCheckConstraint(
                "ck_campaign_upload_row_applied_has_contact",
                "applied_at IS NULL OR contact_id IS NOT NULL");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(r => r.SourceRowNumber).HasColumnName("source_row_number").IsRequired();
        builder.Property(r => r.FirstName).HasColumnName("first_name");
        builder.Property(r => r.LastName).HasColumnName("last_name");
        builder.Property(r => r.Email).HasColumnName("email");
        builder.Property(r => r.Phone).HasColumnName("phone");
        builder.Property(r => r.CompanyRaw).HasColumnName("company_raw");
        builder.Property(r => r.Bucket).HasColumnName("bucket").IsRequired();
        builder.Property(r => r.ReasonCode).HasColumnName("reason_code");
        builder.Property(r => r.ContactId).HasColumnName("contact_id");

        builder.Property(r => r.DuplicateRowNumbers)
            .HasColumnName("duplicate_row_numbers")
            .HasColumnType("integer[]")
            .IsRequired();

        builder.Property(r => r.CollapsedDuplicates).HasColumnName("collapsed_duplicates").IsRequired();
        builder.Property(r => r.AppliedAt).HasColumnName("applied_at");

        // El cursor de la confirmación: las filas de un lote en el orden del archivo, y las que
        // faltan por aplicar. Es la consulta que corre en cada reintento.
        builder.HasIndex(r => new { r.BatchId, r.SourceRowNumber }, "ix_campaign_upload_row_batch_source_row")
            .IsUnique();

        builder.HasOne(r => r.Batch)
            .WithMany(b => b.Rows)
            .HasForeignKey(r => r.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Los valores de uno o más conjuntos cerrados, entrecomillados para un <c>IN</c>.</summary>
    private static string Quoted(params IReadOnlySet<string>[] sets) =>
        string.Join(", ", sets.SelectMany(set => set).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(value => $"'{value}'"));
}
